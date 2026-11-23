using System.Collections.Immutable;
using System.Globalization;
using Lattice.Client;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

using static Lattice.Runtime.SemanticResponseFormatting;
using static Lattice.Runtime.SemanticWorldModelResponseReasoningHelpers;
namespace Lattice.Runtime;

internal static class SemanticDefinitionResponder
{

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

        var relations = reasoner.GetRelations(individual.Id)
            .Where(assertion => assertion.Relation is not ("instance-of" or "is-a"))
            .Select(assertion => (
                Assertion: assertion,
                Relation: model.FindRelation(assertion.Relation),
                Target: model.FindConcept(assertion.Object)))
            .Where(item => item.Relation is not null && item.Target is not null)
            .OrderBy(item => item.Relation!.Order)
            .ThenBy(item => item.Relation!.Id, StringComparer.Ordinal)
            .ThenBy(item => item.Target!.Id, StringComparer.Ordinal)
            .ToArray();

        var documentation = model.GetDataAssertions(individual.Id, "documentation-url");
        if (specificTypes.Length == 0 && relations.Length == 0 && documentation.Length == 0)
        {
            return false;
        }

        var clauses = new List<string>();
        var sources = new List<SemanticWorldModelSource>();

        if (specificTypes.Length > 0)
        {
            clauses.Add(
                $"{individual.Label} is {JoinConditions(specificTypes.Select(DescribeTypeMembership).ToArray())}.");
            sources.AddRange(specificTypes.SelectMany(type =>
                reasoner.GetMembershipEvidence(individual.Id, type!.Id)));
        }

        foreach (var item in relations)
        {
            var relation = item.Relation!;
            var target = item.Target!;
            var objectPhrase = IndefinitePhrase(target.Label);
            if (relation.ObjectRole is not null)
            {
                objectPhrase += $" {relation.ObjectRole}";
            }

            clauses.Add($"It {relation.Verb} {objectPhrase}.");
            sources.Add(item.Assertion.Source);

            if (target.Kind == SemanticConceptKind.Individual && relation.RangeType is not null)
            {
                sources.AddRange(reasoner.GetMembershipEvidence(target.Id, relation.RangeType));
            }
        }

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

    internal static bool TryContributionQuery(
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

    internal static bool TryExplainEventOutcomes(
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
            .OrderBy(assertion => model.FindRelation(assertion.Relation)?.Order ?? int.MaxValue)
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
                "begins" => $"{target} begins",
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
        SemanticRelationAssertion[] appearanceAssertions = appearance is null || horizon is null
            ? []
            : reasoner.GetRelations(appearance.Id, "appears-below")
                .Where(assertion => assertion.Object == horizon.Id)
                .ToArray();
        if (appearanceAssertions.Length > 0)
        {
            sources.AddRange(appearanceAssertions.Select(assertion => assertion.Source));
        }

        var outcomeSentence = $"When {eventConcept.Label} occurs, {string.Join(" and ", clauses)}.";
        var explanation = causeDetails.Count == 0
            ? string.Empty
            : $" This happens because {string.Join(" and ", causeDetails)}.";
        var apparentMotion = appearanceAssertions.Length > 0
            ? " The Sun appears to move below the local horizon; it has not gone out or disappeared."
            : string.Empty;

        message = outcomeSentence + explanation + apparentMotion;
        evidence = ImmutableArray.Create(ToEvidence(
            sources.Distinct().ToImmutableArray(),
            LatticeEvidenceKind.Derived));
        return true;
    }
}
