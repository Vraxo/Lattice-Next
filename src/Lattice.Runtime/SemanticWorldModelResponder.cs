using System.Collections.Immutable;
using Lattice.Client;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

namespace Lattice.Runtime;

internal static class SemanticWorldModelResponder
{
    internal static bool TryRespond(
        SemanticWorldModelQuestionFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(reasoner);

        return request.Kind switch
        {
            SemanticWorldModelQuestionKind.Definition =>
                TryDefinition(request.Concept ?? request.Subject, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.WhyClassification =>
                TryWhy(request.Subject, request.Concept ?? string.Empty, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.CapabilityExplanation =>
                TryExplainCapability(request.Subject, request.Concept ?? string.Empty, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.Mechanism =>
                TryExplainMechanism(request.Subject, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.Causes =>
                TryExplainCauses(request.Subject, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.Compare =>
                TryCompare(request.Subject, request.OtherSubject ?? string.Empty, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.Membership =>
                TryMembership(request.Subject, request.Concept ?? string.Empty, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.InverseRelation =>
                TryInverseRelation(
                    request.Subject,
                    request.Concept ?? string.Empty,
                    request.TargetType,
                    request.OtherSubject,
                    model,
                    reasoner,
                    out message,
                    out evidence),
            SemanticWorldModelQuestionKind.RelationQuery =>
                TryRelationQuery(request.Subject, request.Concept, request.OtherSubject, request.TargetType, model, reasoner, out message, out evidence),
            _ => Fail(out message, out evidence)
        };
    }

    internal static bool TryDefinition(
        string concept,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        var individual = model.FindConcept(concept, SemanticConceptKind.Individual);
        if (individual is not null)
        {
            return TryDescribeIndividual(individual, model, reasoner, out message, out evidence);
        }

        var type = model.FindConcept(concept, SemanticConceptKind.Type);
        var definition = type is null ? null : model.FindDefinition(type.Id);
        if (type is null || definition is null)
        {
            return false;
        }

        var parent = reasoner.GetDirectSupertypes(type.Id)
            .Select(parentId => model.FindConcept(parentId, SemanticConceptKind.Type))
            .FirstOrDefault(candidate => candidate is not null);

        if (definition.DefinitionKind == SemanticDefinitionKind.Primitive)
        {
            var necessaryPhrases = definition.NecessaryConditions
                .Select(condition => FormatCondition(condition, model))
                .Where(phrase => phrase is not null)
                .Select(phrase => phrase!)
                .ToArray();
            var necessaryClause = necessaryPhrases.Length == 0
                ? string.Empty
                : $" Its instances must {JoinConditions(necessaryPhrases)}.";
            message = parent is null
                ? $"{Capitalize(type.Label)} is a primitive root type in the current model; it has no further structural definition.{necessaryClause}"
                : $"{Capitalize(type.Label)} is currently modeled as a primitive subtype of {IndefinitePhrase(parent.Label)}; the model does not claim a sufficient structural definition for it.{necessaryClause}";
            evidence = ImmutableArray.Create(ToEvidence([definition.Source], LatticeEvidenceKind.Asserted));
            return true;
        }

        var expressionPhrase = FormatExpression(definition.SufficientExpression, model);
        if (expressionPhrase is null)
        {
            return false;
        }

        message = parent is null
            ? $"{Capitalize(IndefinitePhrase(type.Label))} is defined by instances that {expressionPhrase}."
            : $"{Capitalize(IndefinitePhrase(type.Label))} is {IndefinitePhrase(parent.Label)} that {expressionPhrase}.";
        evidence = ImmutableArray.Create(ToEvidence([definition.Source], LatticeEvidenceKind.Asserted));
        return true;
    }

    private static bool TryDescribeIndividual(
        SemanticConcept individual,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var types = reasoner.GetTypes(individual.Id);
        var specificTypes = types
            .Where(candidate => !types.Any(other =>
                other != candidate && IsSubtype(other, candidate, model)))
            .Select(type => model.FindConcept(type, SemanticConceptKind.Type))
            .Where(type => type is not null && type.Id is not (
                "entity" or "software" or "capability" or "research-publication"))
            .ToArray();

        if (specificTypes.Length == 0)
        {
            return false;
        }

        var clauses = new List<string>
        {
            $"{individual.Label} is {JoinConditions(specificTypes.Select(DescribeTypeMembership).ToArray())}."
        };
        var sources = specificTypes
            .SelectMany(type => reasoner.GetMembershipEvidence(individual.Id, type!.Id))
            .ToList();

        var capabilityAssertions = reasoner.GetRelations(individual.Id, "supports-capability");
        var capabilities = capabilityAssertions
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .Where(target => target is not null)
            .Select(target => target!)
            .DistinctBy(target => target.Id)
            .ToArray();

        if (capabilities.Length > 0)
        {
            clauses.Add($"It supports {JoinConditions(capabilities.Select(target => target.Label).ToArray())}.");
            sources.AddRange(capabilityAssertions.Select(assertion => assertion.Source));
            foreach (var capability in capabilities)
            {
                sources.AddRange(reasoner.GetMembershipEvidence(capability.Id, "capability"));
            }
        }

        var domainAssertions = reasoner.GetRelations(individual.Id, "used-for-domain");
        var domains = domainAssertions
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .Where(target => target is not null)
            .Select(target => target!)
            .DistinctBy(target => target.Id)
            .ToArray();
        if (domains.Length > 0)
        {
            clauses.Add($"It is used for {JoinConditions(domains.Select(target => target.Label).ToArray())}.");
            sources.AddRange(domainAssertions.Select(assertion => assertion.Source));
        }

        var documentation = model.GetDataAssertions(individual.Id, "documentation-url");
        if (documentation.Length > 0)
        {
            clauses.Add($"Its documentation is available at {documentation[0].Value}.");
            sources.AddRange(documentation.Select(assertion => assertion.Source));
        }

        message = string.Join(" ", clauses);
        evidence = ImmutableArray.Create(ToEvidence(
            sources.Distinct().ToImmutableArray(),
            LatticeEvidenceKind.Derived));
        return true;
    }

    private static string DescribeTypeMembership(SemanticConcept type)
    {
        if (type.Id == "open-source-software")
        {
            return type.Label;
        }

        return IndefinitePhrase(type.Label);
    }

    private static bool TryExplainCapability(
        string requestedSubject,
        string requestedCapability,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        if (!string.Equals(requestedCapability, "survive", StringComparison.Ordinal))
        {
            return false;
        }

        var subjectType = model.FindConcept(requestedSubject, SemanticConceptKind.Type);
        if (subjectType is null)
        {
            return false;
        }

        var individuals = model.Concepts
            .Where(concept =>
                concept.Kind == SemanticConceptKind.Individual &&
                reasoner.IsInstanceOf(concept.Id, subjectType.Id))
            .ToArray();
        var adaptations = individuals
            .SelectMany(individual => reasoner.GetRelations(individual.Id, "has-adaptation"))
            .ToArray();
        var survivalAssertions = adaptations
            .SelectMany(adaptation => reasoner.GetRelations(adaptation.Object, "supports-survival-under"))
            .ToArray();
        var condition = model.FindConcept("water-deprivation-condition", SemanticConceptKind.Individual);
        if (condition is null ||
            !survivalAssertions.Any(assertion => assertion.Object == condition.Id))
        {
            return false;
        }

        var waterConservation = adaptations
            .Where(adaptation => reasoner.IsInstanceOf(adaptation.Object, "water-conservation"))
            .ToArray();
        var processes = waterConservation
            .SelectMany(adaptation => reasoner.GetRelations(adaptation.Object, "reduces-water-loss"))
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .Where(concept => concept is not null)
            .Select(concept => concept!)
            .DistinctBy(concept => concept.Id)
            .ToArray();
        var dehydrationTolerance = adaptations
            .Where(adaptation => reasoner.IsInstanceOf(adaptation.Object, "dehydration-tolerance"))
            .ToArray();

        if (waterConservation.Length == 0 || processes.Length == 0 || dehydrationTolerance.Length == 0)
        {
            return false;
        }

        var processNames = processes
            .Select(process => process.Label.StartsWith($"{subjectType.Label} ", StringComparison.OrdinalIgnoreCase)
                ? process.Label[(subjectType.Label.Length + 1)..]
                : process.Label)
            .ToArray();
        var subjectLabel = subjectType.Label;
        message = $"{Capitalize(subjectLabel)} can survive periods without drinking water because its water-conservation adaptation limits water loss through {JoinConditions(processNames)}. Its dehydration-tolerance adaptation also supports survival during water deprivation.";
        var sources = adaptations.Select(adaptation => adaptation.Source)
            .Concat(survivalAssertions.Select(assertion => assertion.Source))
            .Concat(waterConservation.SelectMany(adaptation =>
                reasoner.GetRelations(adaptation.Object, "reduces-water-loss").Select(assertion => assertion.Source)))
            .Concat(dehydrationTolerance.Select(adaptation => adaptation.Source))
            .Distinct()
            .ToImmutableArray();
        evidence = ImmutableArray.Create(ToEvidence(sources, LatticeEvidenceKind.Derived));
        return true;
    }

    private static bool TryExplainCauses(
        string requestedConcept,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var type = model.FindConcept(requestedConcept, SemanticConceptKind.Type);
        if (type is null)
        {
            return false;
        }

        var instances = model.Concepts
            .Where(concept =>
                concept.Kind == SemanticConceptKind.Individual &&
                reasoner.IsInstanceOf(concept.Id, type.Id))
            .ToArray();
        var mechanisms = instances
            .SelectMany(instance => reasoner.GetRelations(instance.Id, "has-mechanism"))
            .ToArray();
        if (mechanisms.Length == 0)
        {
            return false;
        }

        var labels = mechanisms
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .Where(concept => concept is not null)
            .Select(concept => concept!.Label)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (labels.Length == 0)
        {
            return false;
        }

        var clauses = new List<string>
        {
            $"{Capitalize(type.Label)} can arise through {JoinConditions(labels)}."
        };
        var sources = mechanisms.Select(assertion => assertion.Source)
            .Concat(instances.SelectMany(instance =>
                reasoner.GetMembershipEvidence(instance.Id, type.Id)))
            .ToList();

        var references = instances
            .SelectMany(instance => reasoner.GetRelations(instance.Id, "has-research-reference"))
            .ToArray();
        var referenceDetails = references
            .Select(assertion => FormatResearchReference(assertion.Object, model, reasoner))
            .OfType<string>()
            .ToArray();
        if (referenceDetails.Length > 0)
        {
            clauses.Add($"Research references include {JoinConditions(referenceDetails)}.");
            sources.AddRange(references.Select(assertion => assertion.Source));
            foreach (var reference in references)
            {
                sources.AddRange(reasoner.GetRelations(reference.Object)
                    .Select(assertion => assertion.Source));
                sources.AddRange(model.GetDataAssertions(reference.Object, "doi-url")
                    .Select(assertion => assertion.Source));
            }
        }

        message = string.Join(" ", clauses);
        evidence = ImmutableArray.Create(ToEvidence(sources.Distinct().ToImmutableArray(), LatticeEvidenceKind.Derived));
        return true;
    }

    internal static bool TryClassify(
        MultiItemClassificationFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var itemType = request.ItemType.Value;
        var results = new List<(string Item, ImmutableArray<string> Categories, ImmutableArray<SemanticWorldModelSource> Sources)>();

        foreach (var requestedItem in request.Items)
        {
            var item = model.FindConcept(requestedItem.Value, SemanticConceptKind.Individual);
            if (item is null)
            {
                return false;
            }

            var matching = new List<(string Label, ImmutableArray<SemanticWorldModelSource> Sources)>();
            foreach (var category in request.Categories)
            {
                var categoryType = model.FindConcept(
                    $"{category.Value}-{itemType}",
                    SemanticConceptKind.Type);
                if (categoryType is null)
                {
                    continue;
                }

                var sources = reasoner.GetMembershipEvidence(item.Id, categoryType.Id);
                if (!sources.IsDefaultOrEmpty)
                {
                    matching.Add((category.Value, sources));
                }
            }

            if (matching.Count == 0)
            {
                return false;
            }

            results.Add((
                item.Label,
                matching.Select(item => item.Label).ToImmutableArray(),
                matching.SelectMany(item => item.Sources).Distinct().ToImmutableArray()));
        }

        message = string.Join(
            " ",
            results.Select(result => result.Categories.Length switch
            {
                1 => $"{Capitalize(result.Item)} is {Indefinite(result.Categories[0])} {result.Categories[0]} {itemType}.",
                2 => $"{Capitalize(result.Item)} is both a {result.Categories[0]} and a {result.Categories[1]} {itemType}.",
                _ => $"{Capitalize(result.Item)} belongs to the {JoinNames(result.Categories)} {itemType} categories."
            }));

        evidence = results
            .Select(result => ToEvidence(result.Sources, LatticeEvidenceKind.Derived))
            .ToImmutableArray();
        return true;
    }

    internal static bool TryChoice(
        ChoiceQuestionFormula question,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(reasoner);

        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var category = model.FindConcept(question.Category.Value, SemanticConceptKind.Type);
        if (category is null)
        {
            return false;
        }

        var supported = question.Choices
            .Select(choice => (
                Choice: model.FindConcept(choice.Value, SemanticConceptKind.Individual),
                Evidence: model.FindConcept(choice.Value, SemanticConceptKind.Individual) is { } individual
                    ? reasoner.GetMembershipEvidence(individual.Id, category.Id)
                    : ImmutableArray<SemanticWorldModelSource>.Empty))
            .Where(candidate => candidate.Choice is not null && !candidate.Evidence.IsDefaultOrEmpty)
            .ToArray();

        if (supported.Length != 1)
        {
            return false;
        }

        message = $"{Capitalize(supported[0].Choice!.Label)} is a species of {category.Label}.";
        evidence = ImmutableArray.Create(ToEvidence(
            supported[0].Evidence,
            LatticeEvidenceKind.Derived));
        return true;
    }

    private static bool TryMembership(
        string individualId,
        string typeId,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var individual = model.FindConcept(individualId, SemanticConceptKind.Individual);
        var type = model.FindConcept(typeId, SemanticConceptKind.Type);
        if (individual is null || type is null ||
            !reasoner.IsInstanceOf(individual.Id, type.Id))
        {
            return false;
        }

        var sources = reasoner.GetMembershipEvidence(individual.Id, type.Id);
        if (sources.IsDefaultOrEmpty)
        {
            return false;
        }

        message = $"Yes, {individual.Label} is {IndefinitePhrase(type.Label)}.";
        evidence = ImmutableArray.Create(ToEvidence(sources, LatticeEvidenceKind.Derived));
        return true;
    }

    private static bool TryInverseRelation(
        string objectId,
        string activeVerb,
        string? requestedTargetType,
        string? contextId,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var knownObject = model.FindConcept(objectId, SemanticConceptKind.Individual);
        if (knownObject is null)
        {
            return false;
        }

        var matchingRelations = model.Relations
            .Where(relation =>
                relation.SubjectKind == SemanticRelationArgumentKind.Individual &&
                relation.ObjectKind == SemanticRelationArgumentKind.Individual &&
                relation.Verb.StartsWith("is ", StringComparison.OrdinalIgnoreCase) &&
                relation.Verb.EndsWith(" by", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    relation.Verb[3..^3].Trim(),
                    activeVerb,
                    StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matchingRelations.Length != 1)
        {
            return false;
        }

        var relation = matchingRelations[0];
        var hasRequestedType = !string.IsNullOrWhiteSpace(requestedTargetType);
        var assertions = model.RelationAssertions
            .Where(assertion =>
                assertion.Relation == relation.Id &&
                (hasRequestedType
                    ? assertion.Object == knownObject.Id
                    : assertion.Subject == knownObject.Id))
            .ToArray();

        if (!hasRequestedType)
        {
            var agents = assertions
                .Select(assertion => model.FindConcept(assertion.Subject, SemanticConceptKind.Individual))
                .Where(agent => agent is not null)
                .DistinctBy(agent => agent!.Id)
                .ToArray();
            if (agents.Length == 0)
            {
                return false;
            }

            message = agents.Length == 1
                ? $"{agents[0]!.Label} {activeVerb} {knownObject.Label}."
                : $"{JoinNames(agents.Select(agent => agent!.Label).ToArray())} {activeVerb} {knownObject.Label}.";
            evidence = ImmutableArray.Create(ToEvidence(
                assertions.Select(assertion => assertion.Source).Distinct().ToImmutableArray(),
                LatticeEvidenceKind.Derived));
            return true;
        }

        var targetType = model.FindConcept(requestedTargetType!, SemanticConceptKind.Type);
        var context = string.IsNullOrWhiteSpace(contextId)
            ? null
            : model.FindConcept(contextId, SemanticConceptKind.Individual);
        if (targetType is null || (!string.IsNullOrWhiteSpace(contextId) && context is null))
        {
            return false;
        }

        var candidates = assertions
            .Where(assertion => reasoner.IsInstanceOf(assertion.Subject, targetType.Id))
            .Select(assertion => new
            {
                Concept = model.FindConcept(assertion.Subject, SemanticConceptKind.Individual),
                RelationSource = assertion.Source,
                ContextSources = context is null
                    ? ImmutableArray<SemanticWorldModelSource>.Empty
                    : model.RelationAssertions
                        .Where(contextAssertion =>
                            contextAssertion.Subject == context.Id &&
                            contextAssertion.Object == assertion.Subject)
                        .Where(contextAssertion =>
                        {
                            var contextRelation = model.FindRelation(contextAssertion.Relation);
                            if (contextRelation is null ||
                                contextRelation.SubjectKind != SemanticRelationArgumentKind.Individual ||
                                contextRelation.ObjectKind != SemanticRelationArgumentKind.Individual)
                            {
                                return false;
                            }

                            var domain = model.FindConcept(contextRelation.Domain, SemanticConceptKind.Type);
                            var range = model.FindConcept(contextRelation.Range, SemanticConceptKind.Type);
                            return domain is not null &&
                                range is not null &&
                                reasoner.IsInstanceOf(context.Id, domain.Id) &&
                                reasoner.IsInstanceOf(assertion.Subject, range.Id);
                        })
                        .Select(contextAssertion => contextAssertion.Source)
                        .ToImmutableArray()
            })
            .Where(candidate =>
                candidate.Concept is not null &&
                (context is null || !candidate.ContextSources.IsDefaultOrEmpty))
            .DistinctBy(candidate => candidate.Concept!.Id)
            .ToArray();

        if (candidates.Length == 0)
        {
            return false;
        }

        var passivePhrase = relation.Verb.StartsWith("is ", StringComparison.OrdinalIgnoreCase)
            ? relation.Verb[3..]
            : relation.Verb;
        var names = candidates.Select(candidate => candidate.Concept!.Label).ToArray();
        message = candidates.Length == 1
            ? $"{names[0]} is {IndefinitePhrase(targetType.Label)} {passivePhrase} {knownObject.Label}."
            : $"{JoinNames(names)} are {Pluralize(targetType.Label)} {passivePhrase} {knownObject.Label}.";

        var sources = candidates
            .Select(candidate => candidate.RelationSource)
            .Concat(candidates.SelectMany(candidate => candidate.ContextSources))
            .Distinct()
            .ToImmutableArray();
        evidence = ImmutableArray.Create(ToEvidence(sources, LatticeEvidenceKind.Derived));
        return true;
    }

    private static string Pluralize(string value)
    {
        if (value.EndsWith('y') && value.Length > 1 &&
            !"aeiou".Contains(char.ToLowerInvariant(value[^2])))
        {
            return $"{value[..^1]}ies";
        }

        return value.EndsWith("s", StringComparison.OrdinalIgnoreCase)
            ? $"{value}es"
            : $"{value}s";
    }

    private static bool TryRelationQuery(
        string subjectId,
        string? requestedVerb,
        string? otherSubjectId,
        string? requestedTargetType,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var subject = model.FindConcept(subjectId, SemanticConceptKind.Individual);
        if (subject is null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(otherSubjectId))
        {
            var other = model.FindConcept(otherSubjectId, SemanticConceptKind.Individual);
            if (other is null)
            {
                return false;
            }

            var connectedAssertions = model.RelationAssertions
                .Where(assertion =>
                    (assertion.Subject == subject.Id && assertion.Object == other.Id) ||
                    (assertion.Subject == other.Id && assertion.Object == subject.Id))
                .ToArray();
            if (connectedAssertions.Length > 0)
            {
                var sentences = connectedAssertions
                    .Select(assertion =>
                    {
                        var relation = model.FindRelation(assertion.Relation);
                        var first = model.FindConcept(assertion.Subject, SemanticConceptKind.Individual);
                        var second = model.FindConcept(assertion.Object, SemanticConceptKind.Individual);
                        return relation is null || first is null || second is null
                            ? null
                            : FormatQueryRelation(first, second, relation, model, first: false);
                    })
                    .Where(sentence => sentence is not null)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                if (sentences.Length == 0)
                {
                    return false;
                }

                message = string.Join(" ", sentences);
                evidence = ImmutableArray.Create(ToEvidence(
                    connectedAssertions.Select(assertion => assertion.Source).Distinct().ToImmutableArray(),
                    LatticeEvidenceKind.Derived));
                return true;
            }

            if (!TryFindUniqueShortestPath(subject.Id, other.Id, model, out var path))
            {
                return false;
            }

            var orderedPath = path.All(step => !step.Forward)
                ? path.Reverse()
                : path;
            var pathSentences = orderedPath
                .Select(step =>
                {
                    var assertion = step.Assertion;
                    var relation = model.FindRelation(assertion.Relation);
                    var first = model.FindConcept(assertion.Subject, SemanticConceptKind.Individual);
                    var second = model.FindConcept(assertion.Object, SemanticConceptKind.Individual);
                    return relation is null || first is null || second is null
                        ? null
                        : FormatQueryRelation(first, second, relation, model, first: false);
                })
                .Where(sentence => sentence is not null)
                .ToArray();
            if (pathSentences.Length != path.Length)
            {
                return false;
            }

            message = string.Join(" ", pathSentences);
            evidence = ImmutableArray.Create(ToEvidence(
                path.Select(step => step.Assertion.Source).Distinct().ToImmutableArray(),
                LatticeEvidenceKind.Derived));
            return true;
        }

        if (string.IsNullOrWhiteSpace(requestedVerb))
        {
            return false;
        }

        var targetType = string.IsNullOrWhiteSpace(requestedTargetType)
            ? null
            : model.FindConcept(requestedTargetType, SemanticConceptKind.Type);
        if (!string.IsNullOrWhiteSpace(requestedTargetType) && targetType is null)
        {
            return false;
        }

        var matchingRelations = model.Relations
            .Where(relation =>
                relation.SubjectKind == SemanticRelationArgumentKind.Individual &&
                relation.ObjectKind == SemanticRelationArgumentKind.Individual &&
                MatchesRelationVerb(relation.Verb, requestedVerb))
            .Select(relation => new
            {
                Relation = relation,
                Assertions = model.RelationAssertions
                    .Where(assertion =>
                        assertion.Subject == subject.Id &&
                        assertion.Relation == relation.Id &&
                        (targetType is null || reasoner.IsInstanceOf(assertion.Object, targetType.Id)))
                    .ToArray()
            })
            .Where(candidate => candidate.Assertions.Length > 0)
            .ToArray();
        if (matchingRelations.Length != 1)
        {
            return false;
        }

        var selectedRelation = matchingRelations[0].Relation;
        var assertions = matchingRelations[0].Assertions;
        var targets = assertions
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .Where(target => target is not null)
            .DistinctBy(target => target!.Id)
            .ToArray();
        if (targets.Length == 0)
        {
            return false;
        }

        message = string.Join(
            " ",
            targets.Select((target, index) =>
                FormatQueryRelation(subject, target!, selectedRelation, model, first: index == 0)));
        evidence = ImmutableArray.Create(ToEvidence(
            assertions.Select(assertion => assertion.Source).Distinct().ToImmutableArray(),
            LatticeEvidenceKind.Derived));
        return true;
    }

    private static bool TryFindUniqueShortestPath(
        string start,
        string target,
        SemanticWorldModel model,
        out ImmutableArray<(SemanticRelationAssertion Assertion, bool Forward)> path)
    {
        const int maximumDepth = 3;
        path = [];
        var pending = new Queue<(
            string Node,
            ImmutableArray<(SemanticRelationAssertion Assertion, bool Forward)> Path,
            HashSet<string> Visited)>();
        pending.Enqueue((start, [], new HashSet<string>(StringComparer.Ordinal) { start }));
        int? shortestDepth = null;
        var shortestPaths = new List<ImmutableArray<(SemanticRelationAssertion Assertion, bool Forward)>>();

        while (pending.TryDequeue(out var current))
        {
            if (current.Path.Length >= maximumDepth ||
                (shortestDepth.HasValue && current.Path.Length >= shortestDepth.Value))
            {
                continue;
            }

            foreach (var assertion in model.RelationAssertions.Where(assertion =>
                assertion.Subject == current.Node || assertion.Object == current.Node))
            {
                var relation = model.FindRelation(assertion.Relation);
                if (relation is null ||
                    relation.SubjectKind != SemanticRelationArgumentKind.Individual ||
                    relation.ObjectKind != SemanticRelationArgumentKind.Individual)
                {
                    continue;
                }

                var forward = assertion.Subject == current.Node;
                var nextNode = forward ? assertion.Object : assertion.Subject;
                if (current.Visited.Contains(nextNode))
                {
                    continue;
                }

                var nextPath = current.Path.Add((assertion, forward));
                if (nextNode == target)
                {
                    shortestDepth ??= nextPath.Length;
                    if (nextPath.Length == shortestDepth.Value)
                    {
                        shortestPaths.Add(nextPath);
                    }

                    continue;
                }

                if (!shortestDepth.HasValue || nextPath.Length < shortestDepth.Value)
                {
                    var visited = new HashSet<string>(current.Visited, StringComparer.Ordinal)
                    {
                        nextNode
                    };
                    pending.Enqueue((nextNode, nextPath, visited));
                }
            }
        }

        if (shortestPaths.Count != 1)
        {
            return false;
        }

        path = shortestPaths[0];
        return true;
    }

    private static string FormatQueryRelation(
        SemanticConcept subject,
        SemanticConcept target,
        SemanticRelationDefinition relation,
        SemanticWorldModel model,
        bool first)
    {
        var rangeType = relation.RangeType is null
            ? null
            : model.FindConcept(relation.RangeType, SemanticConceptKind.Type);
        if (relation.ObjectRole is null &&
            rangeType is not null &&
            relation.Verb.EndsWith(rangeType.Label, StringComparison.OrdinalIgnoreCase))
        {
            var verb = relation.Verb[..^rangeType.Label.Length].TrimEnd();
            var prefix = first
                ? Capitalize(IndefinitePhrase(subject.Label))
                : $"The {subject.Label}";
            return $"{prefix} {verb} the {target.Label}.";
        }

        return FormatRelation(subject, target, relation, first);
    }

    private static bool MatchesRelationVerb(string relationVerb, string requestedVerb)
    {
        var requested = NormalizeVerb(requestedVerb);
        if (requested.Length == 0)
        {
            return false;
        }

        var words = relationVerb.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var activeVerb = words.FirstOrDefault(word =>
            !word.Equals("is", StringComparison.OrdinalIgnoreCase) &&
            !word.Equals("are", StringComparison.OrdinalIgnoreCase) &&
            !word.Equals("was", StringComparison.OrdinalIgnoreCase) &&
            !word.Equals("were", StringComparison.OrdinalIgnoreCase));
        return activeVerb is not null &&
            NormalizeVerb(activeVerb).Equals(requested, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeVerb(string verb)
    {
        var normalized = SemanticWorldModel.Normalize(verb);
        if (normalized.EndsWith("ies", StringComparison.Ordinal) && normalized.Length > 3)
        {
            return normalized[..^3] + "y";
        }

        if (normalized.EndsWith('s') && normalized.Length > 1)
        {
            return normalized[..^1];
        }

        return normalized;
    }

    internal static bool TryAskMembership(
        string individual,
        string type,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var item = model.FindConcept(individual, SemanticConceptKind.Individual);
        var targetType = model.FindConcept(type, SemanticConceptKind.Type);
        if (item is null || targetType is null)
        {
            return false;
        }

        var sources = reasoner.GetMembershipEvidence(item.Id, targetType.Id);
        if (sources.IsDefaultOrEmpty)
        {
            return false;
        }

        evidence = ImmutableArray.Create(ToEvidence(sources, LatticeEvidenceKind.Derived));
        return true;
    }

    private static bool TryWhy(
        string individualId,
        string typeId,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var individual = model.FindConcept(individualId, SemanticConceptKind.Individual);
        var type = model.FindConcept(typeId, SemanticConceptKind.Type);
        var definition = type is null ? null : model.FindDefinition(type.Id);
        if (individual is null ||
            type is null ||
            definition?.SufficientExpression is null ||
            !TryExplainExpression(
                individual.Id,
                definition.SufficientExpression,
                model,
                reasoner,
                out var phrases,
                out var sources))
        {
            return false;
        }

        message = $"{Capitalize(IndefinitePhrase(individual.Label))} is {IndefinitePhrase(type.Label)} because it {JoinConditions(phrases)}.";
        sources = ImmutableArray.Create(definition.Source).AddRange(sources);
        evidence = ImmutableArray.Create(ToEvidence(sources.Distinct().ToImmutableArray(), LatticeEvidenceKind.Derived));
        return true;
    }

    private static bool TryExplainExpression(
        string individual,
        SemanticClassExpression expression,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out ImmutableArray<string> phrases,
        out ImmutableArray<SemanticWorldModelSource> sources)
    {
        phrases = [];
        sources = [];
        if (expression.Kind == SemanticClassExpressionKind.Some)
        {
            foreach (var candidate in reasoner.GetRelations(individual, expression.Relation))
            {
                if (!reasoner.IsInstanceOf(candidate.Object, expression.TargetType!))
                {
                    continue;
                }

                var nestedPhrases = ImmutableArray<string>.Empty;
                var nestedSources = ImmutableArray<SemanticWorldModelSource>.Empty;
                if (expression.Filler is not null &&
                    !TryExplainExpression(
                        candidate.Object,
                        expression.Filler,
                        model,
                        reasoner,
                        out nestedPhrases,
                        out nestedSources))
                {
                    continue;
                }

                var phrase = FormatCondition(
                    new SemanticRestriction(expression.Relation!, expression.TargetType!),
                    model);
                if (phrase is null)
                {
                    return false;
                }

                if (!nestedPhrases.IsDefaultOrEmpty)
                {
                    phrase += $" that {JoinConditions(nestedPhrases)}";
                }

                phrases = [phrase];
                sources = ImmutableArray.Create(candidate.Source)
                    .AddRange(reasoner.GetMembershipEvidence(candidate.Object, expression.TargetType!))
                    .AddRange(nestedSources);
                return true;
            }

            return false;
        }

        if (expression.Kind == SemanticClassExpressionKind.Or)
        {
            foreach (var operand in expression.Operands)
            {
                if (TryExplainExpression(individual, operand, model, reasoner, out phrases, out sources))
                {
                    return true;
                }
            }

            return false;
        }

        var phraseBuilder = ImmutableArray.CreateBuilder<string>();
        var sourceBuilder = ImmutableArray.CreateBuilder<SemanticWorldModelSource>();
        foreach (var operand in expression.Operands)
        {
            if (!TryExplainExpression(individual, operand, model, reasoner, out var childPhrases, out var childSources))
            {
                return false;
            }

            phraseBuilder.AddRange(childPhrases);
            sourceBuilder.AddRange(childSources);
        }

        phrases = phraseBuilder.ToImmutable();
        sources = sourceBuilder.ToImmutable();
        return true;
    }

    private static bool TryExplainMechanism(
        string individualId,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var individual = model.FindConcept(individualId, SemanticConceptKind.Individual);
        if (individual is null)
        {
            return false;
        }

        var mechanismAssertions = reasoner.GetRelations(individual.Id, "has-sound-production-mechanism");
        if (mechanismAssertions.IsDefaultOrEmpty)
        {
            return false;
        }

        var sentences = new List<string>();
        var sources = new List<SemanticWorldModelSource>();
        var visited = new HashSet<string>(StringComparer.Ordinal);

        foreach (var mechanism in mechanismAssertions)
        {
            var relation = model.FindRelation(mechanism.Relation);
            var target = model.FindConcept(mechanism.Object, SemanticConceptKind.Individual);
            if (relation is null || target is null)
            {
                continue;
            }

            sentences.Add(FormatRelation(individual, target, relation, first: true));
            sources.Add(mechanism.Source);
            AppendMechanismChain(target.Id, model, reasoner, visited, sentences, sources, 0);
        }

        if (sentences.Count == 0)
        {
            return false;
        }

        message = string.Join(" ", sentences);
        evidence = ImmutableArray.Create(ToEvidence(sources.Distinct().ToImmutableArray(), LatticeEvidenceKind.Derived));
        return true;
    }

    private static void AppendMechanismChain(
        string subjectId,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        HashSet<string> visited,
        List<string> sentences,
        List<SemanticWorldModelSource> sources,
        int depth)
    {
        if (depth >= 6 || !visited.Add(subjectId))
        {
            return;
        }

        foreach (var assertion in reasoner.GetRelations(subjectId)
            .Where(assertion => (model.FindRelation(assertion.Relation)?.Order ?? 0) > 0))
        {
            var relation = model.FindRelation(assertion.Relation);
            var subject = model.FindConcept(assertion.Subject, SemanticConceptKind.Individual);
            var target = model.FindConcept(assertion.Object, SemanticConceptKind.Individual);
            if (relation is null || subject is null || target is null)
            {
                continue;
            }

            sentences.Add(FormatRelation(subject, target, relation, first: false));
            sources.Add(assertion.Source);
            AppendMechanismChain(target.Id, model, reasoner, visited, sentences, sources, depth + 1);
        }
    }

    private static bool TryCompare(
        string firstId,
        string secondId,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var first = model.FindConcept(firstId, SemanticConceptKind.Individual);
        var second = model.FindConcept(secondId, SemanticConceptKind.Individual);
        if (first is null || second is null)
        {
            return false;
        }

        var clauses = new List<string>();
        var sources = new List<SemanticWorldModelSource>();
        foreach (var relation in model.Relations
            .Where(relation => relation.ComparisonPriority > 0)
            .OrderBy(relation => relation.ComparisonPriority))
        {
            var firstFacts = reasoner.GetRelations(first.Id, relation.Id);
            var secondFacts = reasoner.GetRelations(second.Id, relation.Id);
            if (firstFacts.IsDefaultOrEmpty || secondFacts.IsDefaultOrEmpty)
            {
                continue;
            }

            var firstObject = model.FindConcept(firstFacts[0].Object, SemanticConceptKind.Individual);
            var secondObject = model.FindConcept(secondFacts[0].Object, SemanticConceptKind.Individual);
            if (firstObject is null || secondObject is null)
            {
                continue;
            }

            var firstType = GetMostSpecificType(firstObject.Id, model, reasoner);
            var secondType = GetMostSpecificType(secondObject.Id, model, reasoner);
            if (firstType is null || secondType is null)
            {
                continue;
            }

            if (firstType.Id == secondType.Id)
            {
                var verb = relation.Verb == "has" ? "have" : relation.Verb;
                var role = relation.ObjectRole?.Replace("its", "their", StringComparison.Ordinal) ?? string.Empty;
                clauses.Add($"Both {verb} {IndefinitePhrase(firstType.Label)} {role}".TrimEnd());
            }
            else
            {
                clauses.Add($"{Capitalize(first.Label)} {relation.Verb} {IndefinitePhrase(firstType.Label)}, while {second.Label} {relation.Verb} {IndefinitePhrase(secondType.Label)}");
            }

            sources.Add(firstFacts[0].Source);
            sources.Add(secondFacts[0].Source);
        }

        if (clauses.Count == 0)
        {
            return false;
        }

        message = string.Join(". ", clauses).TrimEnd('.') + ".";
        evidence = ImmutableArray.Create(ToEvidence(sources.Distinct().ToImmutableArray(), LatticeEvidenceKind.Derived));
        return true;
    }

    private static SemanticConcept? GetMostSpecificType(
        string individual,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner)
    {
        var types = reasoner.GetTypes(individual);
        return types
            .Where(type => !types.Any(other =>
                other != type && IsSubtype(other, type, model)))
            .Select(type => model.FindConcept(type, SemanticConceptKind.Type))
            .Where(type => type is not null)
            .OrderBy(type => type!.Label, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static bool IsSubtype(string candidate, string expected, SemanticWorldModel model)
    {
        if (candidate == expected)
        {
            return true;
        }

        var pending = new Stack<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        pending.Push(candidate);
        while (pending.TryPop(out var current))
        {
            if (!visited.Add(current))
            {
                continue;
            }

            foreach (var parent in model.RelationAssertions
                .Where(assertion => assertion.Subject == current && assertion.Relation == "is-a")
                .Select(assertion => assertion.Object))
            {
                if (parent == expected)
                {
                    return true;
                }

                pending.Push(parent);
            }
        }

        return false;
    }

    private static string? FormatResearchReference(
        string publicationId,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner)
    {
        var publication = model.FindConcept(publicationId, SemanticConceptKind.Individual);
        if (publication is null)
        {
            return null;
        }

        var authors = reasoner.GetRelations(publicationId, "authored-by")
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .Where(author => author is not null)
            .Select(author => author!.Label.Split(' ', StringSplitOptions.RemoveEmptyEntries)[^1])
            .ToArray();
        var year = reasoner.GetRelations(publicationId, "published-in-year")
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .FirstOrDefault(concept => concept is not null);
        var journal = reasoner.GetRelations(publicationId, "published-in")
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .FirstOrDefault(concept => concept is not null);
        var citation = authors.Length == 0
            ? publication.Label
            : year is null
                ? $"{JoinNames(authors)}, {publication.Label}"
                : $"{JoinNames(authors)} ({year.Label}), {publication.Label}";
        if (journal is not null)
        {
            citation += $", {journal.Label}";
        }

        return citation;
    }

    private static ImmutableArray<SemanticWorldModelSource> GetTypeEvidence(
        string individual,
        string type,
        SemanticWorldModel model) =>
        model.RelationAssertions
            .Where(assertion =>
                assertion.Subject == individual &&
                assertion.Relation == "instance-of" &&
                IsSubtype(assertion.Object, type, model))
            .Select(assertion => assertion.Source)
            .ToImmutableArray();

    private static string? FormatExpression(
        SemanticClassExpression? expression,
        SemanticWorldModel model)
    {
        if (expression is null)
        {
            return null;
        }

        if (expression.Kind == SemanticClassExpressionKind.Some)
        {
            var phrase = FormatCondition(
                new SemanticRestriction(expression.Relation!, expression.TargetType!),
                model);
            if (phrase is null || expression.Filler is null)
            {
                return phrase;
            }

            var filler = FormatExpression(expression.Filler, model);
            return filler is null ? null : $"{phrase} that {filler}";
        }

        var phrases = expression.Operands
            .Select(operand => FormatExpression(operand, model))
            .ToArray();
        if (phrases.Any(phrase => phrase is null))
        {
            return null;
        }

        var validPhrases = phrases.Select(phrase => phrase!).ToArray();
        return expression.Kind == SemanticClassExpressionKind.And
            ? JoinConditions(validPhrases)
            : JoinAlternatives(validPhrases);
    }

    private static string JoinAlternatives(IReadOnlyList<string> phrases) =>
        phrases.Count switch
        {
            1 => phrases[0],
            2 => $"{phrases[0]} or {phrases[1]}",
            _ => $"{string.Join(", ", phrases.Take(phrases.Count - 1))}, or {phrases[^1]}"
        };

    private static string? FormatCondition(SemanticRestriction condition, SemanticWorldModel model)
    {
        var relation = model.FindRelation(condition.Relation);
        var target = model.FindConcept(condition.TargetType, SemanticConceptKind.Type);
        if (relation is null || target is null)
        {
            return null;
        }

        var phrase = $"{relation.Verb} {IndefinitePhrase(target.Label)}";
        return relation.ObjectRole is null
            ? phrase
            : $"{phrase} {relation.ObjectRole}";
    }

    private static string FormatRelation(
        SemanticConcept subject,
        SemanticConcept target,
        SemanticRelationDefinition relation,
        bool first)
    {
        var prefix = first ? Capitalize(IndefinitePhrase(subject.Label)) : $"The {subject.Label}";
        var objectPhrase = relation.ObjectRole is null
            ? $"the {target.Label}"
            : $"{IndefinitePhrase(target.Label)} {relation.ObjectRole}";
        var verb = subject.Label.EndsWith('s') && subject.Label.Length > 1
            ? InflectPluralSubjectVerb(relation.Verb)
            : relation.Verb;
        return $"{prefix} {verb} {objectPhrase}.";
    }

    private static string FormatRelationTarget(SemanticRelationDefinition relation, string targetLabel) =>
        relation.ObjectRole is null
            ? $"{relation.Verb} {Indefinite(targetLabel)} {targetLabel}"
            : $"{relation.Verb} {Indefinite(targetLabel)} {targetLabel} {relation.ObjectRole}";

    private static string InflectPluralSubjectVerb(string verbPhrase)
    {
        var separator = verbPhrase.IndexOf(' ');
        var verb = separator < 0 ? verbPhrase : verbPhrase[..separator];
        var remainder = separator < 0 ? string.Empty : verbPhrase[separator..];
        var baseVerb = verb switch
        {
            "has" => "have",
            "does" => "do",
            _ when verb.EndsWith("ies", StringComparison.Ordinal) => $"{verb[..^3]}y",
            _ when verb.EndsWith("s", StringComparison.Ordinal) => verb[..^1],
            _ => verb
        };
        return $"{baseVerb}{remainder}";
    }

    private static string JoinConditions(IReadOnlyList<string> phrases) =>
        phrases.Count switch
        {
            1 => phrases[0],
            2 => $"{phrases[0]} and {phrases[1]}",
            _ => $"{string.Join(", ", phrases.Take(phrases.Count - 1))}, and {phrases[^1]}"
        };

    private static string JoinNames(IReadOnlyList<string> names) =>
        names.Count switch
        {
            0 => string.Empty,
            1 => names[0],
            2 => $"{names[0]} and {names[1]}",
            _ => $"{string.Join(", ", names.Take(names.Count - 1))}, and {names[^1]}"
        };

    private static string Indefinite(string phrase) =>
        IsMassNoun(phrase)
            ? string.Empty
            : phrase.Length > 0 && "aeiou".Contains(char.ToLowerInvariant(phrase[0]))
                ? "an"
                : "a";

    private static string IndefinitePhrase(string phrase)
    {
        var article = Indefinite(phrase);
        return article.Length == 0 ? phrase : $"{article} {phrase}";
    }

    private static bool IsMassNoun(string phrase) =>
        phrase is "monopsony power" or "buyer market power" or "market power" or "software" or "straightness";

    private static string Capitalize(string value) =>
        value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];

    private static LatticeEvidence ToEvidence(
        ImmutableArray<SemanticWorldModelSource> sources,
        LatticeEvidenceKind kind) =>
        new(
            kind,
            LatticeEvidencePolarity.Positive,
            sources.Select(source => new LatticeEvidenceSource(
                LatticeKnowledgeSourceKind.FoundationalKnowledge,
                source.SourceId,
                source.SourceId,
                null,
                null)).ToImmutableArray(),
            ImmutableArray<LatticeEvidence>.Empty);

    private static bool Fail(
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        return false;
    }
}
