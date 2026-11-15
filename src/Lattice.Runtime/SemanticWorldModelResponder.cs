using System.Collections.Immutable;
using System.Globalization;
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
            SemanticWorldModelQuestionKind.Ability =>
                TryExplainAbility(request.Subject, request.Concept ?? string.Empty, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.Mechanism =>
                TryExplainMechanism(request.Subject, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.Causes =>
                TryExplainCauses(request.Subject, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.Compare =>
                TryCompare(request.Subject, request.OtherSubject ?? string.Empty, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.ContributionQuery =>
                TryContributionQuery(request, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.WhyHarmful =>
                TryExplainHarmfulness(request.Subject, request.Concept ?? string.Empty, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.BeginnerGuidance =>
                TryBeginnerGuidance(request.Subject, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.CreativeIdeas =>
                TryCreativeIdeas(request.Subject, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.ListExamples =>
                TryListExamples(request.Subject, request.Limit, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.MaximumDataValue =>
                TryMaximumDataValue(request, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.EventOutcomes =>
                TryExplainEventOutcomes(request.Subject, model, reasoner, out message, out evidence),
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
        var scopeEvidence = ImmutableArray<LatticeEvidence>.Empty;
        if (request.Scope is not null)
        {
            var scope = model.FindConcept(request.Scope, SemanticConceptKind.Individual);
            if (scope is null)
            {
                return false;
            }

            var scopeAssertions = model.Relations
                .SelectMany(relation => reasoner.GetRelations(scope.Id, relation.Id))
                .ToArray();
            var scopedTargetIds = scopeAssertions
                .Select(assertion => assertion.Object)
                .ToHashSet(StringComparer.Ordinal);
            matches = matches
                .Where(match => scopedTargetIds.Contains(match.Target!.Id))
                .ToArray();
            if (matches.Length == 0)
            {
                return false;
            }

            scopeEvidence = ImmutableArray.Create(ToEvidence(
                scopeAssertions
                    .Where(assertion => matches.Any(match => match.Target!.Id == assertion.Object))
                    .Select(assertion => assertion.Source)
                    .Distinct()
                    .ToImmutableArray(),
                LatticeEvidenceKind.Derived));
        }

        if (matches.Length == 0)
        {
            return false;
        }

        var relation = matches[0].Relation;
        var targets = matches
            .Select(match => match.Target!)
            .DistinctBy(target => target.Id)
            .ToArray();
        var targetPhrase = request.Scope is null
            ? JoinNames(targets.Select(target => $"the {target.Label}").ToArray())
            : JoinNames(targets.Select(target => target.Label).ToArray());
        message = request.Scope is null
            ? $"The {subject.Label} {relation.Verb} {targetPhrase}."
            : $"{Capitalize(subject.Label)} {relation.Verb} {targetPhrase}.";
        var relationEvidence = ToEvidence(
            matches.Select(match => match.Source).Distinct().ToImmutableArray(),
            LatticeEvidenceKind.Derived);
        evidence = scopeEvidence.IsDefaultOrEmpty
            ? ImmutableArray.Create(relationEvidence)
            : scopeEvidence.Add(relationEvidence);
        return true;
    }

    internal static bool TryRespond(
        SemanticRelationSourceQueryFormula request,
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
        var target = model.FindConcept(request.Object, SemanticConceptKind.Individual);
        if (target is null)
        {
            return false;
        }

        var matchingRelationIds = model.Relations
            .Where(relation => MatchesVerb(relation.Verb, request.Verb))
            .Select(relation => relation.Id)
            .ToHashSet(StringComparer.Ordinal);
        if (matchingRelationIds.Count == 0)
        {
            return false;
        }

        var matches = model.RelationAssertions
            .Where(assertion =>
                assertion.Object == target.Id &&
                matchingRelationIds.Contains(assertion.Relation))
            .Select(assertion => (
                Subject: model.FindConcept(assertion.Subject, SemanticConceptKind.Individual),
                Relation: model.FindRelation(assertion.Relation),
                assertion.Source))
            .Where(match =>
                match.Subject is not null &&
                match.Relation is not null &&
                (request.SubjectType is null ||
                 reasoner.IsInstanceOf(match.Subject.Id, request.SubjectType)))
            .ToArray();
        if (matches.Length == 0)
        {
            return false;
        }

        var subjects = matches
            .Select(match => match.Subject!)
            .DistinctBy(subject => subject.Id)
            .ToArray();
        var relation = matches[0].Relation!;
        var subjectPhrase = JoinNames(subjects.Select(subject =>
            request.SubjectType is null ? $"the {subject.Label}" : subject.Label).ToArray());
        var pluralSubject = subjects.Length > 1 ||
            subjects[0].Label.EndsWith("s", StringComparison.OrdinalIgnoreCase);
        var verb = pluralSubject
            ? NormalizeVerb(relation.Verb.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0])
            : relation.Verb;
        var objectPhrase = request.SubjectType is null ? $"the {target.Label}" : target.Label;
        message = $"{Capitalize(subjectPhrase)} {verb} {objectPhrase}.";
        var sources = matches
            .Select(match => match.Source)
            .Concat(request.SubjectType is null
                ? []
                : subjects.SelectMany(subject =>
                    reasoner.GetMembershipEvidence(subject.Id, request.SubjectType)))
            .Distinct()
            .ToImmutableArray();
        evidence = ImmutableArray.Create(ToEvidence(sources, LatticeEvidenceKind.Derived));
        return true;
    }

    private static bool MatchesVerb(string relationVerb, string requestedVerb)
    {
        var relationHead = relationVerb.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        return string.Equals(
            NormalizeVerb(relationHead),
            NormalizeVerb(requestedVerb),
            StringComparison.Ordinal);
    }

    private static string NormalizeVerb(string value)
    {
        var verb = SemanticWorldModel.Normalize(value);
        return verb switch
        {
            "has" => "have",
            "does" => "do",
            _ when verb.EndsWith("ies", StringComparison.Ordinal) => $"{verb[..^3]}y",
            _ when verb.EndsWith("ed", StringComparison.Ordinal) => verb[..^2],
            _ when verb.EndsWith("s", StringComparison.Ordinal) => verb[..^1],
            _ => verb
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

    private static bool TryContributionQuery(
        SemanticWorldModelQuestionFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        var recipient = model.FindConcept(request.Subject, SemanticConceptKind.Individual);
        var resourceType = model.FindConcept(request.Concept ?? string.Empty, SemanticConceptKind.Type);
        var location = model.FindConcept(request.OtherSubject ?? string.Empty, SemanticConceptKind.Individual);
        var facilityType = model.FindConcept(request.TargetType ?? string.Empty, SemanticConceptKind.Type);
        if (recipient is null || resourceType is null || location is null || facilityType is null)
        {
            return false;
        }

        var headquartersAssertions = model.RelationAssertions
            .Where(assertion =>
                assertion.Subject == recipient.Id &&
                assertion.Relation == "has-headquarters-in" &&
                assertion.Object == location.Id)
            .ToArray();
        if (headquartersAssertions.Length == 0)
        {
            return false;
        }

        var siteAssertions = model.RelationAssertions
            .Where(assertion =>
                assertion.Relation == "situated-in" &&
                assertion.Object == location.Id)
            .Select(assertion => (
                Site: model.FindConcept(assertion.Subject, SemanticConceptKind.Individual),
                Assertion: assertion))
            .Where(candidate =>
                candidate.Site is not null &&
                reasoner.IsInstanceOf(candidate.Site.Id, "site") &&
                reasoner.IsInstanceOf(candidate.Site.Id, resourceType.Id) &&
                reasoner.IsInstanceOf(candidate.Site.Id, facilityType.Id))
            .ToArray();
        if (siteAssertions.Length != 1)
        {
            return false;
        }

        var site = siteAssertions[0].Site!;
        var contributions = model.RelationAssertions
            .Where(assertion =>
                assertion.Object == site.Id &&
                assertion.Relation is "funded-acquisition-of" or "contributed-property-to")
            .Select(assertion => (
                Contributor: model.FindConcept(assertion.Subject, SemanticConceptKind.Individual),
                Relation: model.FindRelation(assertion.Relation),
                Assertion: assertion))
            .Where(item => item.Contributor is not null && item.Relation is not null)
            .OrderBy(item => item.Assertion.Relation == "funded-acquisition-of" ? 0 : 1)
            .ToArray();
        if (contributions.Length == 0)
        {
            return false;
        }

        var clauses = contributions
            .Select(item => $"{item.Contributor!.Label} {item.Relation!.Verb} the {site.Label}.")
            .ToArray();
        message = string.Join(" ", clauses);

        var sources = headquartersAssertions
            .Select(assertion => assertion.Source)
            .Concat(siteAssertions.Select(candidate => candidate.Assertion.Source))
            .Concat(contributions.Select(item => item.Assertion.Source))
            .Concat(reasoner.GetMembershipEvidence(site.Id, "site"))
            .Concat(reasoner.GetMembershipEvidence(site.Id, resourceType.Id))
            .Concat(reasoner.GetMembershipEvidence(site.Id, facilityType.Id))
            .Distinct()
            .ToImmutableArray();
        evidence = ImmutableArray.Create(ToEvidence(sources, LatticeEvidenceKind.Derived));
        return true;
    }

    private static bool TryExplainEventOutcomes(
        string eventId,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var eventConcept = model.FindConcept(eventId, SemanticConceptKind.Individual);
        if (eventConcept is null)
        {
            return false;
        }

        var effects = reasoner.GetRelations(eventConcept.Id)
            .Where(assertion => assertion.Relation is "ends" or "begins" or "results-in" or "leads-to")
            .Select(assertion => (
                Relation: model.FindRelation(assertion.Relation),
                Target: model.FindConcept(assertion.Object, SemanticConceptKind.Individual),
                assertion.Source))
            .Where(effect => effect.Relation is not null && effect.Target is not null)
            .ToArray();
        if (effects.Length == 0)
        {
            return false;
        }

        var clauses = effects.Select(effect =>
        {
            var verb = effect.Relation!.Verb;
            var target = effect.Target!.Label;
            return effect.Relation.Id switch
            {
                "ends" => $"daylight ends",
                "begins" => $"{Capitalize(target)} begins",
                "results-in" => $"it results in {target}",
                "leads-to" => $"it leads to {target}",
                _ => $"{verb} {target}"
            };
        }).ToArray();

        var causes = model.RelationAssertions
            .Where(assertion => assertion.Object == eventConcept.Id && assertion.Relation == "causes")
            .Select(assertion => (
                Cause: model.FindConcept(assertion.Subject, SemanticConceptKind.Individual),
                Assertion: assertion))
            .Where(item => item.Cause is not null)
            .ToArray();

        var causeDetails = new List<string>();
        var sources = effects.Select(effect => effect.Source).ToList();
        foreach (var cause in causes)
        {
            sources.Add(cause.Assertion.Source);
            var turning = reasoner.GetRelations(cause.Cause!.Id, "turns-location-away-from");
            foreach (var assertion in turning)
            {
                var target = model.FindConcept(assertion.Object, SemanticConceptKind.Individual);
                if (target is null)
                {
                    continue;
                }

                var relation = model.FindRelation(assertion.Relation);
                if (relation is null)
                {
                    continue;
                }

                causeDetails.Add($"{cause.Cause.Label} {relation.Verb} {target.Label}");
                sources.Add(assertion.Source);
            }
        }

        var appearance = model.FindConcept("sun", SemanticConceptKind.Individual);
        var horizon = model.FindConcept("local-horizon", SemanticConceptKind.Individual);
        var appearanceAssertions = appearance is null || horizon is null
            ? []
            : reasoner.GetRelations(appearance.Id, "appears-below")
                .Where(assertion => assertion.Object == horizon.Id)
                .ToArray();
        if (appearanceAssertions.Length > 0)
        {
            sources.AddRange(appearanceAssertions.Select(assertion => assertion.Source));
        }

        var outcomeSentence = $"{Capitalize(eventConcept.Label)} occurs: {string.Join(" and ", clauses)}.";
        var explanation = causeDetails.Count == 0
            ? string.Empty
            : $" This is due to {string.Join(" and ", causeDetails)}.";
        var apparentMotion = appearanceAssertions.Length > 0
            ? " The Sun appears to move below the local horizon, but it has not gone out or disappeared."
            : string.Empty;

        message = outcomeSentence + explanation + apparentMotion;
        evidence = ImmutableArray.Create(ToEvidence(
            sources.Distinct().ToImmutableArray(),
            LatticeEvidenceKind.Derived));
        return true;
    }

    private static bool TryMaximumDataValue(
        SemanticWorldModelQuestionFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        var property = model.FindDataProperty(request.Concept ?? string.Empty);
        if (property is null || property.ValueKind != SemanticDataValueKind.Number)
        {
            return false;
        }

        var candidates = model.Concepts
            .Where(concept =>
                concept.Kind == SemanticConceptKind.Individual &&
                reasoner.IsInstanceOf(concept.Id, property.DomainType))
            .SelectMany(concept => model.GetDataAssertions(concept.Id, property.Id)
                .Where(assertion => decimal.TryParse(
                    assertion.Value,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out _))
                .Select(assertion => (
                    Concept: concept,
                    Assertion: assertion,
                    Value: decimal.Parse(
                        assertion.Value,
                        NumberStyles.Number,
                        CultureInfo.InvariantCulture))))
            .ToArray();
        if (candidates.Length == 0)
        {
            return false;
        }

        var maximum = candidates.Max(candidate => candidate.Value);
        var winners = candidates
            .Where(candidate => candidate.Value == maximum)
            .ToArray();
        var names = JoinNames(winners.Select(winner => winner.Concept.Label).ToArray());
        var verb = winners.Length == 1 ? "has" : "have";
        var metric = string.IsNullOrWhiteSpace(request.OtherSubject)
            ? property.Id.Replace('-', ' ')
            : request.OtherSubject;
        message = $"{names} {verb} won the most {metric} ({maximum.ToString(CultureInfo.InvariantCulture)}).";
        evidence = ImmutableArray.Create(ToEvidence(
            winners.Select(winner => winner.Assertion.Source).Distinct().ToImmutableArray(),
            LatticeEvidenceKind.Derived));
        return true;
    }

    private static bool TryListExamples(
        string requestedType,
        int limit,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        if (limit is < 1 or > 10)
        {
            return false;
        }

        var type = model.FindConcept(requestedType, SemanticConceptKind.Type);
        if (type is null)
        {
            return false;
        }

        var examples = model.Concepts
            .Where(concept =>
                concept.Kind == SemanticConceptKind.Individual &&
                reasoner.IsInstanceOf(concept.Id, type.Id))
            .OrderBy(concept => concept.Label, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToArray();
        if (examples.Length < limit)
        {
            return false;
        }

        var sources = examples
            .SelectMany(example => reasoner.GetMembershipEvidence(example.Id, type.Id))
            .Distinct()
            .ToImmutableArray();
        if (sources.IsDefaultOrEmpty)
        {
            return false;
        }

        var pluralLabel = type.Label.EndsWith("y", StringComparison.OrdinalIgnoreCase)
            ? $"{type.Label[..^1]}ies"
            : $"{type.Label}s";
        message = $"Here are {limit} prominent {pluralLabel}:\n{string.Join("\n", examples.Select((example, index) => $"{index + 1}. {example.Label}"))}";
        evidence = ImmutableArray.Create(ToEvidence(sources, LatticeEvidenceKind.Derived));
        return true;
    }

    private static bool TryCreativeIdeas(
        string requestedCategory,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        var category = model.FindConcept(requestedCategory, SemanticConceptKind.Individual);
        if (category is null)
        {
            return false;
        }

        var ideaAssertions = reasoner.GetRelations(category.Id, "has-diy-idea");
        if (ideaAssertions.IsDefaultOrEmpty)
        {
            return false;
        }

        var ideas = new List<(string Label, string Materials, string Method, string Attachment)>();
        var sources = new List<SemanticWorldModelSource>();
        foreach (var assertion in ideaAssertions)
        {
            var idea = model.FindConcept(assertion.Object, SemanticConceptKind.Individual);
            if (idea is null)
            {
                continue;
            }

            var materials = reasoner.GetRelations(idea.Id, "uses-material")
                .Select(item => model.FindConcept(item.Object, SemanticConceptKind.Individual))
                .Where(item => item is not null)
                .Select(item => item!.Label)
                .ToArray();
            var methods = reasoner.GetRelations(idea.Id, "made-by-method")
                .Select(item => model.FindConcept(item.Object, SemanticConceptKind.Individual))
                .Where(item => item is not null)
                .Select(item => item!.Label)
                .ToArray();
            var attachments = reasoner.GetRelations(idea.Id, "attaches-with")
                .Select(item => model.FindConcept(item.Object, SemanticConceptKind.Individual))
                .Where(item => item is not null)
                .Select(item => item!.Label)
                .ToArray();
            if (materials.Length == 0 || methods.Length == 0 || attachments.Length == 0)
            {
                continue;
            }

            ideas.Add((
                idea.Label,
                JoinConditions(materials),
                JoinConditions(methods),
                JoinConditions(attachments)));
            sources.Add(assertion.Source);
            sources.AddRange(reasoner.GetRelations(idea.Id, "uses-material").Select(item => item.Source));
            sources.AddRange(reasoner.GetRelations(idea.Id, "made-by-method").Select(item => item.Source));
            sources.AddRange(reasoner.GetRelations(idea.Id, "attaches-with").Select(item => item.Source));
        }

        if (ideas.Count == 0)
        {
            return false;
        }

        var suggestions = ideas.Select((idea, index) =>
            $"{index + 1}. **{Capitalize(idea.Label)}** — Materials: {idea.Materials}. Make it by {idea.Method}; attach it with {IndefinitePhrase(idea.Attachment)}.");
        message = $"Here are some DIY ideas for {category.Label}:\n{string.Join("\n", suggestions)}";
        evidence = ImmutableArray.Create(ToEvidence(
            sources.Distinct().ToImmutableArray(),
            LatticeEvidenceKind.Derived));
        return true;
    }

    private static bool TryBeginnerGuidance(
        string requestedActivity,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        var activity = model.FindConcept(requestedActivity, SemanticConceptKind.Individual);
        if (activity is null || !reasoner.IsInstanceOf(activity.Id, "physical-activity"))
        {
            return false;
        }

        var firstSteps = reasoner.GetRelations(activity.Id, "has-first-beginner-step");
        if (firstSteps.Length != 1)
        {
            return false;
        }

        var steps = new List<(SemanticConcept Step, SemanticConcept Purpose)>();
        var sources = new List<SemanticWorldModelSource>
        {
            firstSteps[0].Source
        };
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var currentId = firstSteps[0].Object;

        while (true)
        {
            if (!visited.Add(currentId))
            {
                return false;
            }

            var step = model.FindConcept(currentId, SemanticConceptKind.Individual);
            if (step is null || !reasoner.IsInstanceOf(step.Id, "beginner-practice"))
            {
                return false;
            }

            var purposes = reasoner.GetRelations(step.Id, "has-practice-purpose");
            if (purposes.Length != 1)
            {
                return false;
            }

            var purpose = model.FindConcept(purposes[0].Object, SemanticConceptKind.Individual);
            if (purpose is null || !reasoner.IsInstanceOf(purpose.Id, "training-purpose"))
            {
                return false;
            }

            steps.Add((step, purpose));
            sources.AddRange(reasoner.GetMembershipEvidence(step.Id, "beginner-practice"));
            sources.AddRange(reasoner.GetMembershipEvidence(purpose.Id, "training-purpose"));
            sources.Add(purposes[0].Source);

            var nextSteps = reasoner.GetRelations(step.Id, "followed-by");
            if (nextSteps.Length == 0)
            {
                break;
            }

            if (nextSteps.Length != 1)
            {
                return false;
            }

            sources.Add(nextSteps[0].Source);
            currentId = nextSteps[0].Object;
        }

        if (steps.Count == 0)
        {
            return false;
        }

        var safetyAssertions = reasoner.GetRelations(activity.Id, "has-safety-guidance");
        var safetyGuidance = safetyAssertions
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .Where(concept => concept is not null)
            .Select(concept => concept!)
            .ToArray();
        var warningAssertions = reasoner.GetRelations(activity.Id, "stop-and-seek-help-for");
        var warningGuidance = warningAssertions
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .Where(concept => concept is not null)
            .Select(concept => concept!)
            .ToArray();
        if (safetyGuidance.Length == 0 || warningGuidance.Length == 0)
        {
            return false;
        }

        sources.AddRange(safetyAssertions.Select(assertion => assertion.Source));
        sources.AddRange(warningAssertions.Select(assertion => assertion.Source));
        sources.AddRange(safetyGuidance.Concat(warningGuidance).SelectMany(concept =>
            reasoner.GetMembershipEvidence(concept.Id, "safety-guidance")));
        sources.AddRange(reasoner.GetMembershipEvidence(activity.Id, "physical-activity"));

        var stepText = string.Join("\n", steps.Select((item, index) =>
            $"{index + 1}. {Capitalize(item.Step.Label)}; purpose: {item.Purpose.Label}."));
        var warningIds = warningGuidance.Select(concept => concept.Id)
            .ToHashSet(StringComparer.Ordinal);
        var safetyText = JoinConditions(safetyGuidance
            .Where(concept => !warningIds.Contains(concept.Id))
            .Select(concept => concept.Label)
            .ToArray());
        var warningText = JoinConditions(warningGuidance.Select(concept => concept.Label).ToArray());
        message = $"To start {activity.Label}, use a gradual beginner routine:\n{stepText}\nSafety considerations include {safetyText}. Stop and seek medical help if you experience {warningText}.";
        evidence = ImmutableArray.Create(ToEvidence(
            sources.Distinct().ToImmutableArray(),
            LatticeEvidenceKind.Derived));
        return true;
    }

    private static bool TryExplainHarmfulness(
        string requestedSubject,
        string requestedTarget,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        var subject = model.FindConcept(requestedSubject, SemanticConceptKind.Individual);
        var targetType = model.FindConcept(requestedTarget, SemanticConceptKind.Type);
        var effectType = model.FindConcept("adverse-effect", SemanticConceptKind.Type);
        if (subject is null ||
            targetType is null ||
            effectType is null ||
            !reasoner.IsInstanceOf(subject.Id, "technology-use"))
        {
            return false;
        }

        var effectAssertions = reasoner.GetRelations(subject.Id, "can-contribute-to")
            .Select(assertion => (
                Effect: model.FindConcept(assertion.Object, SemanticConceptKind.Individual),
                Assertion: assertion))
            .Where(candidate =>
                candidate.Effect is not null &&
                reasoner.IsInstanceOf(candidate.Effect.Id, effectType.Id) &&
                reasoner.GetRelations(candidate.Effect.Id, "affects-person")
                    .Any(assertion => assertion.Object == targetType.Id))
            .ToArray();
        if (effectAssertions.Length == 0)
        {
            return false;
        }

        var descriptions = new List<string>();
        var sources = new List<SemanticWorldModelSource>();
        foreach (var candidate in effectAssertions)
        {
            var mechanismAssertions = reasoner.GetRelations(candidate.Effect!.Id, "can-arise-through");
            var mechanisms = mechanismAssertions
                .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
                .Where(concept => concept is not null)
                .Select(concept => concept!)
                .DistinctBy(concept => concept.Id)
                .ToArray();
            if (mechanisms.Length == 0)
            {
                continue;
            }

            descriptions.Add(
                $"{candidate.Effect.Label} (which can arise through {JoinConditions(mechanisms.Select(item => item.Label).ToArray())})");
            sources.Add(candidate.Assertion.Source);
            sources.AddRange(reasoner.GetMembershipEvidence(candidate.Effect.Id, effectType.Id));
            sources.AddRange(reasoner.GetRelations(candidate.Effect.Id, "affects-person")
                .Where(assertion => assertion.Object == targetType.Id)
                .Select(assertion => assertion.Source));
            sources.AddRange(mechanismAssertions.Select(assertion => assertion.Source));
            sources.AddRange(mechanisms.SelectMany(mechanism =>
                reasoner.GetMembershipEvidence(mechanism.Id, "adverse-effect-mechanism")));
        }

        if (descriptions.Count == 0)
        {
            return false;
        }

        message =
            $"{Capitalize(subject.Label)} is not inherently bad for people, but excessive or poorly timed use can contribute to {JoinConditions(descriptions.ToArray())}. These risks depend on usage patterns and can often be reduced through breaks, notification limits, comfortable viewing habits, and keeping phones away from bedtime.";
        evidence = ImmutableArray.Create(ToEvidence(
            sources.Distinct().ToImmutableArray(),
            LatticeEvidenceKind.Derived));
        return true;
    }

    private static bool TryExplainAbility(
        string requestedSubject,
        string requestedAbility,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        var subject = model.FindConcept(requestedSubject, SemanticConceptKind.Type);
        var ability = model.FindConcept(requestedAbility, SemanticConceptKind.Individual);
        if (subject is null || ability is null)
        {
            return false;
        }

        var adaptations = reasoner.GetRelations(subject.Id, "has-adaptation")
            .Select(assertion => (
                Concept: model.FindConcept(assertion.Object, SemanticConceptKind.Individual),
                Assertion: assertion))
            .Where(candidate => candidate.Concept is not null)
            .Select(candidate => (
                Concept: candidate.Concept!,
                Assertion: candidate.Assertion,
                AbilityAssertion: reasoner.GetRelations(candidate.Concept!.Id, "enables-ability")
                    .FirstOrDefault(assertion => assertion.Object == ability.Id)))
            .Where(candidate => candidate.AbilityAssertion is not null)
            .ToArray();

        if (adaptations.Length == 0)
        {
            return false;
        }

        var labels = adaptations
            .Select(candidate => candidate.Concept.Label)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var sources = adaptations
            .SelectMany(candidate => new[]
            {
                candidate.Assertion.Source,
                candidate.AbilityAssertion!.Source
            })
            .Concat(adaptations.SelectMany(candidate =>
                reasoner.GetMembershipEvidence(candidate.Concept.Id, "survival-adaptation")))
            .Concat(reasoner.GetMembershipEvidence(ability.Id, "biological-ability"))
            .Distinct()
            .ToImmutableArray();

        message = $"{Capitalize(requestedSubject)} can {ability.Label} because {string.Join("; ", labels)}.";
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
