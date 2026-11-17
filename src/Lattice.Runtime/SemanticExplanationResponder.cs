using System.Collections.Immutable;
using System.Globalization;
using Lattice.Client;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

namespace Lattice.Runtime;
using static Lattice.Runtime.SemanticWorldModelResponseFormatting;
using static Lattice.Runtime.SemanticWorldModelResponseReasoningHelpers;

internal static class SemanticExplanationResponder
{

internal static bool TryExplainHarmfulness(
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

internal static bool TryExplainAbility(
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

internal static bool TryExplainCapability(
        string requestedSubject,
        string requestedCapability,
        string? requestedContext,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        if (!string.Equals(requestedCapability, "survive", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(requestedContext) ||
            !requestedContext.Contains("water", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var subjectType = model.FindConcept(requestedSubject, SemanticConceptKind.Type);
        var ability = model.FindConcept("survive-for-long-without-water", SemanticConceptKind.Individual);
        var condition = model.FindConcept("water-deprivation-condition", SemanticConceptKind.Individual);
        if (subjectType is null || ability is null || condition is null)
        {
            return false;
        }

        var adaptations = reasoner.GetRelations(subjectType.Id, "has-adaptation")
            .Select(assertion => (
                Concept: model.FindConcept(assertion.Object, SemanticConceptKind.Individual),
                Assertion: assertion))
            .Where(candidate => candidate.Concept is not null)
            .Select(candidate => (
                Concept: candidate.Concept!,
                candidate.Assertion,
                AbilityAssertion: reasoner.GetRelations(candidate.Concept!.Id, "enables-ability")
                    .FirstOrDefault(assertion => assertion.Object == ability.Id),
                ConditionAssertion: reasoner.GetRelations(candidate.Concept!.Id, "supports-survival-under")
                    .FirstOrDefault(assertion => assertion.Object == condition.Id)))
            .Where(candidate =>
                candidate.AbilityAssertion is not null &&
                candidate.ConditionAssertion is not null)
            .ToArray();

        var waterConservation = adaptations
            .Where(candidate => reasoner.IsInstanceOf(candidate.Concept.Id, "water-conservation"))
            .ToArray();
        var dehydrationTolerance = adaptations
            .Where(candidate => reasoner.IsInstanceOf(candidate.Concept.Id, "dehydration-tolerance-adaptation"))
            .ToArray();
        var processes = waterConservation
            .SelectMany(candidate => reasoner.GetRelations(candidate.Concept.Id, "reduces-water-loss"))
            .Select(assertion => (
                Concept: model.FindConcept(assertion.Object, SemanticConceptKind.Individual),
                assertion.Source))
            .Where(candidate => candidate.Concept is not null)
            .ToArray();

        if (waterConservation.Length == 0 ||
            dehydrationTolerance.Length == 0 ||
            processes.Length == 0)
        {
            return false;
        }

        var processNames = processes
            .Select(process => process.Concept!.Label)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var adaptationDetails = waterConservation
            .Select(candidate => candidate.Concept.Label)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var dehydrationDetails = dehydrationTolerance
            .Select(candidate => candidate.Concept.Label)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var subjectLabel = subjectType.Label.EndsWith("s", StringComparison.OrdinalIgnoreCase)
            ? subjectType.Label
            : $"{subjectType.Label}s";
        message = $"{subjectLabel} can survive periods without drinking water because their water-conservation adaptations limit water loss through {JoinConditions(processNames)}. In practice, {JoinConditions(adaptationDetails)}. Their dehydration-tolerance adaptation also supports survival during water deprivation: {JoinConditions(dehydrationDetails)}.";

        var sources = adaptations
            .SelectMany(candidate => new[]
            {
                candidate.Assertion.Source,
                candidate.AbilityAssertion!.Source,
                candidate.ConditionAssertion!.Source
            })
            .Concat(processes.Select(process => process.Source))
            .Concat(waterConservation.SelectMany(candidate =>
                reasoner.GetMembershipEvidence(candidate.Concept.Id, "water-conservation")))
            .Concat(dehydrationTolerance.SelectMany(candidate =>
                reasoner.GetMembershipEvidence(candidate.Concept.Id, "dehydration-tolerance-adaptation")))
            .Concat(reasoner.GetMembershipEvidence(ability.Id, "biological-ability"))
            .Distinct()
            .ToImmutableArray();
        evidence = ImmutableArray.Create(ToEvidence(sources, LatticeEvidenceKind.Derived));
        return true;
    }

internal static bool TryExplainCauses(
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
}
