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
            SemanticWorldModelQuestionKind.Mechanism =>
                TryExplainMechanism(request.Subject, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.Causes =>
                TryExplainCauses(request.Subject, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.Compare =>
                TryCompare(request.Subject, request.OtherSubject ?? string.Empty, model, reasoner, out message, out evidence),
            _ => Fail(out message, out evidence)
        };
    }

    internal static bool TryRespond(
        SemanticRelationQueryFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(reasoner);

        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var subject = model.FindConcept(request.Subject, SemanticConceptKind.Individual);
        if (subject is null)
        {
            return false;
        }

        var matchingRelations = model.Relations
            .Where(relation => MatchesVerb(relation.Verb, request.Verb))
            .ToArray();
        if (matchingRelations.Length == 0)
        {
            return false;
        }

        var matches = matchingRelations
            .SelectMany(relation => reasoner.GetRelations(subject.Id, relation.Id)
                .Select(assertion => (Relation: relation, Assertion: assertion)))
            .Select(match => (
                match.Relation,
                Target: model.FindConcept(match.Assertion.Object, SemanticConceptKind.Individual),
                match.Assertion.Source))
            .Where(match => match.Target is not null)
            .ToArray();
        if (matches.Length == 0)
        {
            return false;
        }

        var relation = matches[0].Relation;
        var targets = matches
            .Select(match => match.Target!)
            .DistinctBy(target => target.Id)
            .ToArray();
        var verb = relation.Verb;
        var targetPhrase = JoinNames(targets.Select(target => $"the {target.Label}").ToArray());
        message = $"The {subject.Label} {verb} {targetPhrase}.";
        evidence = ImmutableArray.Create(ToEvidence(
            matches.Select(match => match.Source).Distinct().ToImmutableArray(),
            LatticeEvidenceKind.Derived));
        return true;
    }

    private static bool MatchesVerb(string relationVerb, string requestedVerb)
    {
        var relationHead = relationVerb.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        var requested = SemanticWorldModel.Normalize(requestedVerb);
        var baseVerb = relationHead switch
        {
            "has" => "have",
            "does" => "do",
            _ when relationHead.EndsWith("ies", StringComparison.Ordinal) => $"{relationHead[..^3]}y",
            _ when relationHead.EndsWith("s", StringComparison.Ordinal) => relationHead[..^1],
            _ => relationHead
        };

        return string.Equals(baseVerb, requested, StringComparison.Ordinal);
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
            var item = model.FindConcept(requestedItem.Value);
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

                var sources = item.Kind switch
                {
                    SemanticConceptKind.Individual =>
                        reasoner.GetMembershipEvidence(item.Id, categoryType.Id),
                    SemanticConceptKind.Type =>
                        GetSubtypeEvidence(item.Id, categoryType.Id, model),
                    _ => ImmutableArray<SemanticWorldModelSource>.Empty
                };
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

    private static ImmutableArray<SemanticWorldModelSource> GetSubtypeEvidence(
        string candidate,
        string expected,
        SemanticWorldModel model)
    {
        if (candidate == expected)
        {
            return [];
        }

        var pending = new Queue<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal) { candidate };
        var previous = new Dictionary<string, SemanticRelationAssertion>(StringComparer.Ordinal);
        pending.Enqueue(candidate);

        while (pending.TryDequeue(out var current))
        {
            foreach (var assertion in model.RelationAssertions.Where(assertion =>
                assertion.Subject == current &&
                assertion.Relation == "is-a"))
            {
                if (!visited.Add(assertion.Object))
                {
                    continue;
                }

                previous[assertion.Object] = assertion;
                if (assertion.Object == expected)
                {
                    var sources = new List<SemanticWorldModelSource>();
                    var type = expected;
                    while (type != candidate)
                    {
                        var step = previous[type];
                        sources.Add(step.Source);
                        type = step.Subject;
                    }

                    sources.Reverse();
                    return sources.ToImmutableArray();
                }

                pending.Enqueue(assertion.Object);
            }
        }

        return [];
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

        if (expression.Kind == SemanticClassExpressionKind.All)
        {
            var relation = model.FindRelation(expression.Relation!);
            var target = model.FindConcept(expression.TargetType!, SemanticConceptKind.Type);
            if (relation is null || target is null)
            {
                return false;
            }

            var assertionSources = ImmutableArray.CreateBuilder<SemanticWorldModelSource>();
            foreach (var candidate in reasoner.GetRelations(individual, expression.Relation))
            {
                if (!reasoner.IsInstanceOf(candidate.Object, expression.TargetType!))
                {
                    return false;
                }

                assertionSources.Add(candidate.Source);
                assertionSources.AddRange(reasoner.GetMembershipEvidence(candidate.Object, expression.TargetType!));
                if (expression.Filler is not null)
                {
                    if (!TryExplainExpression(candidate.Object, expression.Filler, model, reasoner,
                        out _, out var nestedSources))
                    {
                        return false;
                    }

                    assertionSources.AddRange(nestedSources);
                }
            }

            var phrase = $"{relation.Verb} only elements of type {target.Label}";
            if (expression.Filler is not null)
            {
                var fillerPhrase = FormatExpression(expression.Filler, model);
                if (fillerPhrase is null)
                {
                    return false;
                }

                phrase += $" that {fillerPhrase}";
            }

            phrases = [phrase];
            sources = assertionSources.Distinct().ToImmutableArray();
            return true;
        }

        if (expression.Kind == SemanticClassExpressionKind.Cycle)
        {
            var startRelation = model.FindRelation(expression.StartRelation!);
            var traversalRelation = model.FindRelation(expression.Relation!);
            var targetType = model.FindConcept(expression.TargetType!, SemanticConceptKind.Type);
            var sourceConnection = expression.SourceConnectionRelation is null
                ? null
                : model.FindRelation(expression.SourceConnectionRelation);
            var targetConnection = expression.TargetConnectionRelation is null
                ? null
                : model.FindRelation(expression.TargetConnectionRelation);
            var cycleSources = reasoner.GetCycleEvidence(individual, expression);
            if (startRelation is null || traversalRelation is null ||
                targetType is null || cycleSources.IsDefaultOrEmpty)
            {
                return false;
            }

            phrases =
            [
                $"{startRelation.Verb} {IndefinitePhrase(targetType.Label)} and follows {traversalRelation.Verb} links for at least {expression.MinimumLength} elements before returning to that same element" +
                (sourceConnection is null || targetConnection is null
                    ? string.Empty
                    : $", with each transition matching {sourceConnection.Verb} to {targetConnection.Verb} by identity")
            ];
            sources = cycleSources;
            return true;
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

        if (expression.Kind == SemanticClassExpressionKind.All)
        {
            var relation = model.FindRelation(expression.Relation!);
            var target = model.FindConcept(expression.TargetType!, SemanticConceptKind.Type);
            if (relation is null || target is null)
            {
                return null;
            }

            var phrase = $"{relation.Verb} only elements of type {target.Label}";
            if (expression.Filler is null)
            {
                return phrase;
            }

            var filler = FormatExpression(expression.Filler, model);
            return filler is null ? null : $"{phrase} that {filler}";
        }

        if (expression.Kind == SemanticClassExpressionKind.Cycle)
        {
            var start = model.FindRelation(expression.StartRelation!);
            var traversal = model.FindRelation(expression.Relation!);
            var target = model.FindConcept(expression.TargetType!, SemanticConceptKind.Type);
            var sourceConnection = expression.SourceConnectionRelation is null
                ? null
                : model.FindRelation(expression.SourceConnectionRelation);
            var targetConnection = expression.TargetConnectionRelation is null
                ? null
                : model.FindRelation(expression.TargetConnectionRelation);
            return start is null || traversal is null || target is null
                ? null
                : $"{start.Verb} {IndefinitePhrase(target.Label)} and follows {traversal.Verb} links for at least {expression.MinimumLength} elements before returning to that same element" +
                  (sourceConnection is null || targetConnection is null
                      ? string.Empty
                      : $", with each transition matching {sourceConnection.Verb} to {targetConnection.Verb} by identity");
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
