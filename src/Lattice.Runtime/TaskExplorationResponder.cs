using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Lattice.Client;
using Lattice.Core.Generation;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

namespace Lattice.Runtime;

using static Lattice.Runtime.TaskRequestResponderHelpers;

internal static class TaskExplorationResponder
{
    internal static bool TryExploreHobby(
        TaskRequestFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        if (request.Context != "hobby" || request.OutputFormat != "starter-activities")
        {
            return false;
        }

        var hobby = model.FindConcept(request.Subject, SemanticConceptKind.Individual);
        if (hobby is null || !reasoner.IsInstanceOf(hobby.Id, "hobby"))
        {
            return false;
        }

        var activityAssertions = reasoner.GetRelations(hobby.Id, "hobby-has-starter-activity");
        var activities = activityAssertions
            .Select(assertion => new
            {
                Assertion = assertion,
                Activity = model.FindConcept(assertion.Object, SemanticConceptKind.Individual),
                Order = model.GetDataAssertions(assertion.Object, "activity-order").FirstOrDefault()?.Value
            })
            .Select(item => int.TryParse(
                item.Order,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var order) && item.Activity is not null
                    ? new { item.Assertion, Activity = item.Activity, Order = order }
                    : null)
            .Where(item => item is not null)
            .OrderBy(item => item!.Order)
            .ToArray();

        if (activities.Length == 0)
        {
            return false;
        }

        var paragraphs = new List<string>
        {
            $"Here are some approachable ways to explore {hobby.Label}, starting with simpler activities:"
        };
        var sources = new List<SemanticWorldModelSource>();
        sources.AddRange(reasoner.GetMembershipEvidence(hobby.Id, "hobby"));
        sources.AddRange(activityAssertions.Select(assertion => assertion.Source));

        foreach (var item in activities)
        {
            var activity = item!.Activity!;
            var goalAssertions = reasoner.GetRelations(activity.Id, "activity-has-goal");
            var equipmentAssertions = reasoner.GetRelations(activity.Id, "activity-uses-equipment");
            var levelAssertions = reasoner.GetRelations(activity.Id, "activity-has-level");
            var goals = goalAssertions
                .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
                .ToArray();
            var equipment = equipmentAssertions
                .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
                .ToArray();
            var levels = levelAssertions
                .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
                .ToArray();

            if (goals.Length == 0 || goals.Any(goal => goal is null) ||
                equipment.Length == 0 || equipment.Any(item => item is null) ||
                levels.Length == 0 || levels.Any(level => level is null))
            {
                return false;
            }

            var goalText = string.Join("; ", goals.Select(goal => goal!.Label));
            var equipmentText = string.Join(", ", equipment.Select(item => item!.Label));
            var levelText = string.Join(", ", levels.Select(level => level!.Label));
            paragraphs.Add(
                $"**{activity.Label}** ({levelText}): {goalText}. Equipment: {equipmentText}.");

            sources.Add(item.Assertion.Source);
            sources.AddRange(model.GetDataAssertions(activity.Id, "activity-order")
                .Select(assertion => assertion.Source));
            sources.AddRange(reasoner.GetMembershipEvidence(activity.Id, "hobby-activity"));
            sources.AddRange(goalAssertions.Select(assertion => assertion.Source));
            sources.AddRange(equipmentAssertions.Select(assertion => assertion.Source));
            sources.AddRange(levelAssertions.Select(assertion => assertion.Source));
            foreach (var assertion in goalAssertions)
            {
                sources.AddRange(reasoner.GetMembershipEvidence(assertion.Object, "hobby-goal"));
            }

            foreach (var assertion in equipmentAssertions)
            {
                sources.AddRange(reasoner.GetMembershipEvidence(assertion.Object, "hobby-equipment"));
            }

            foreach (var assertion in levelAssertions)
            {
                sources.AddRange(reasoner.GetMembershipEvidence(assertion.Object, "skill-level"));
            }
        }

        var safetyAssertions = reasoner.GetRelations(hobby.Id, "hobby-has-safety-note");
        foreach (var assertion in safetyAssertions)
        {
            var note = model.FindConcept(assertion.Object, SemanticConceptKind.Individual);
            if (note is null)
            {
                return false;
            }

            paragraphs.Add($"**Safety:** {EnsureSentenceEnding(note.Label)}");
            sources.Add(assertion.Source);
            sources.AddRange(reasoner.GetMembershipEvidence(note.Id, "hobby-safety-note"));
        }

        message = string.Join(Environment.NewLine + Environment.NewLine, paragraphs);
        evidence = ImmutableArray.Create(new LatticeEvidence(
            LatticeEvidenceKind.Derived,
            LatticeEvidencePolarity.Positive,
            sources
                .DistinctBy(source => source.SourceId)
                .Select(ToEvidenceSource)
                .ToImmutableArray(),
            ImmutableArray<LatticeEvidence>.Empty));
        return true;
    }

    internal static bool TryBrainstormUses(
        TaskRequestFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        if (request.Scope != "potential-uses" || request.OutputFormat is null)
        {
            return false;
        }

        var separatorIndex = request.OutputFormat.IndexOf('-');
        if (separatorIndex <= 0 ||
            !int.TryParse(request.OutputFormat[..separatorIndex], out var requestedCount) ||
            requestedCount is < 1 or > 10)
        {
            return false;
        }

        var objectType = model.FindConcept(request.Subject, SemanticConceptKind.Type);
        var requestedBrand = request.Context is null
            ? null
            : model.FindConcept(request.Context, SemanticConceptKind.Individual);
        if (objectType is null || (request.Context is not null && requestedBrand is null))
        {
            return false;
        }

        var target = model.Concepts
            .Where(concept => concept.Kind == SemanticConceptKind.Individual &&
                reasoner.IsInstanceOf(concept.Id, objectType.Id))
            .FirstOrDefault(concept =>
                reasoner.GetRelations(concept.Id, "has-state")
                    .Any(assertion => assertion.Object == "empty-state") &&
                (requestedBrand is null || reasoner.GetRelations(concept.Id, "has-brand")
                    .Any(assertion => assertion.Object == requestedBrand.Id)));
        if (target is null)
        {
            return false;
        }

        var targetFeatureAssertions = reasoner.GetRelations(target.Id, "has-feature");
        var targetFeatureIds = targetFeatureAssertions
            .Select(assertion => assertion.Object)
            .ToHashSet(StringComparer.Ordinal);
        var ideas = model.Concepts
            .Where(concept => concept.Kind == SemanticConceptKind.Individual &&
                reasoner.IsInstanceOf(concept.Id, "repurposing-idea"))
            .Select(idea => new
            {
                Idea = idea,
                ObjectAssertion = reasoner.GetRelations(idea.Id, "idea-for-object")
                    .FirstOrDefault(assertion => assertion.Object == objectType.Id),
                Order = model.GetDataAssertions(idea.Id, "idea-order").FirstOrDefault()?.Value
            })
            .Select(candidate => int.TryParse(
                candidate.Order,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var order) && candidate.ObjectAssertion is not null
                    ? new { candidate.Idea, ObjectAssertion = candidate.ObjectAssertion, Order = order }
                    : null)
            .Where(candidate => candidate is not null)
            .OrderBy(candidate => candidate!.Order)
            .Take(requestedCount)
            .ToArray();

        if (ideas.Length != requestedCount)
        {
            return false;
        }

        var paragraphs = new List<string>
        {
            $"Here are {requestedCount} potential uses for an {target.Label}:"
        };
        var sources = new List<SemanticWorldModelSource>();
        sources.AddRange(reasoner.GetMembershipEvidence(target.Id, objectType.Id));
        sources.AddRange(reasoner.GetRelations(target.Id, "has-state")
            .Where(assertion => assertion.Object == "empty-state")
            .Select(assertion => assertion.Source));
        if (requestedBrand is not null)
        {
            sources.AddRange(reasoner.GetRelations(target.Id, "has-brand")
                .Where(assertion => assertion.Object == requestedBrand.Id)
                .Select(assertion => assertion.Source));
        }

        for (var index = 0; index < ideas.Length; index++)
        {
            var candidate = ideas[index]!;
            var purposeAssertion = reasoner.GetRelations(candidate.Idea.Id, "idea-has-purpose")
                .FirstOrDefault();
            var purpose = purposeAssertion is null
                ? null
                : model.FindConcept(purposeAssertion.Object, SemanticConceptKind.Individual);
            var featureAssertions = reasoner.GetRelations(candidate.Idea.Id, "idea-exploits-feature");
            var actionAssertions = reasoner.GetRelations(candidate.Idea.Id, "idea-requires-action");
            var safetyAssertions = reasoner.GetRelations(candidate.Idea.Id, "idea-has-safety-note");
            var actions = actionAssertions
                .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
                .ToArray();
            var safetyNotes = safetyAssertions
                .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
                .ToArray();

            if (purpose is null || featureAssertions.IsDefaultOrEmpty ||
                featureAssertions.Any(assertion => !targetFeatureIds.Contains(assertion.Object)) ||
                actions.Length == 0 || actions.Any(action => action is null) ||
                safetyNotes.Any(note => note is null))
            {
                return false;
            }

            var preparation = string.Join("; ", actions.Select(action => action!.Label));
            var paragraph = $"**{index + 1}. {candidate.Idea.Label}:** Use the can to {purpose.Label}. Preparation: {EnsureSentenceEnding(preparation)}";
            if (safetyNotes.Length > 0)
            {
                paragraph += " Safety: " + string.Join(" ", safetyNotes.Select(note => EnsureSentenceEnding(note!.Label)));
            }

            paragraphs.Add(paragraph);
            sources.Add(candidate.ObjectAssertion!.Source);
            sources.AddRange(model.GetDataAssertions(candidate.Idea.Id, "idea-order")
                .Select(assertion => assertion.Source));
            sources.Add(purposeAssertion!.Source);
            sources.AddRange(reasoner.GetMembershipEvidence(candidate.Idea.Id, "repurposing-idea"));
            sources.AddRange(reasoner.GetMembershipEvidence(purpose.Id, "reuse-purpose"));
            sources.AddRange(featureAssertions.Select(assertion => assertion.Source));
            sources.AddRange(targetFeatureAssertions
                .Where(assertion => featureAssertions.Any(feature => feature.Object == assertion.Object))
                .Select(assertion => assertion.Source));
            sources.AddRange(actionAssertions.Select(assertion => assertion.Source));
            sources.AddRange(safetyAssertions.Select(assertion => assertion.Source));
            foreach (var featureAssertion in featureAssertions)
            {
                sources.AddRange(reasoner.GetMembershipEvidence(featureAssertion.Object, "object-feature"));
            }

            foreach (var actionAssertion in actionAssertions)
            {
                sources.AddRange(reasoner.GetMembershipEvidence(actionAssertion.Object, "reuse-action"));
            }

            foreach (var safetyAssertion in safetyAssertions)
            {
                sources.AddRange(reasoner.GetMembershipEvidence(safetyAssertion.Object, "reuse-safety-note"));
            }
        }

        var generalSafetyNotes = reasoner.GetRelations(target.Id, "object-has-safety-note");
        foreach (var assertion in generalSafetyNotes)
        {
            var note = model.FindConcept(assertion.Object, SemanticConceptKind.Individual);
            if (note is null)
            {
                return false;
            }

            paragraphs.Add($"**General safety:** {EnsureSentenceEnding(note.Label)}");
            sources.Add(assertion.Source);
            sources.AddRange(reasoner.GetMembershipEvidence(note.Id, "reuse-safety-note"));
        }

        message = string.Join(Environment.NewLine + Environment.NewLine, paragraphs);
        evidence = ImmutableArray.Create(new LatticeEvidence(
            LatticeEvidenceKind.Derived,
            LatticeEvidencePolarity.Positive,
            sources
                .DistinctBy(source => source.SourceId)
                .Select(ToEvidenceSource)
                .ToImmutableArray(),
            ImmutableArray<LatticeEvidence>.Empty));
        return true;
    }
}
