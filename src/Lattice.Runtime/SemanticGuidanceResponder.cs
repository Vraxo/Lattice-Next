using System.Collections.Immutable;
using System.Globalization;
using Lattice.Client;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

using static Lattice.Runtime.SemanticResponseFormatting;
namespace Lattice.Runtime;

internal static class SemanticGuidanceResponder
{

    internal static bool TryCreativeIdeas(
        string requestedCategory,
        string? companyName,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        if (requestedCategory == "business-ideas")
        {
            return TryBusinessIdeas(companyName, model, reasoner, out message, out evidence);
        }

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

    private static bool TryBusinessIdeas(
        string? companyName,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        var category = model.FindConcept("business-ideas", SemanticConceptKind.Individual);
        if (category is null)
        {
            return false;
        }

        var ideaAssertions = reasoner.GetRelations(category.Id, "has-business-idea");
        if (ideaAssertions.IsDefaultOrEmpty)
        {
            return false;
        }

        var ideas = new List<(string Name, string Customer, string Problem, string Offering, string Revenue)>();
        var sources = new List<SemanticWorldModelSource>();
        foreach (var assertion in ideaAssertions)
        {
            var idea = model.FindConcept(assertion.Object, SemanticConceptKind.Individual);
            if (idea is null ||
                !TryGetBusinessIdeaDetail(idea.Id, "serves-customer", out var customer, out var customerSource) ||
                !TryGetBusinessIdeaDetail(idea.Id, "addresses-problem", out var problem, out var problemSource) ||
                !TryGetBusinessIdeaDetail(idea.Id, "offers-service", out var offering, out var offeringSource) ||
                !TryGetBusinessIdeaDetail(idea.Id, "earns-revenue-through", out var revenue, out var revenueSource))
            {
                continue;
            }

            ideas.Add((idea.Label, customer, problem, offering, revenue));
            sources.Add(assertion.Source);
            sources.Add(customerSource);
            sources.Add(problemSource);
            sources.Add(offeringSource);
            sources.Add(revenueSource);
        }

        if (ideas.Count == 0)
        {
            return false;
        }

        var company = string.IsNullOrWhiteSpace(companyName)
            ? "the company"
            : companyName.Trim();
        var suggestions = ideas.Select((idea, index) =>
            $"{index + 1}. **{Capitalize(idea.Name)}** — Customers: {idea.Customer}. Problem: {idea.Problem}. Offer: {idea.Offering}. Revenue: {idea.Revenue}.");
        message = $"The prompt does not specify what {company} does yet. Here are some possible directions it could explore:\n{string.Join("\n", suggestions)}\n\nThese are proposals, not facts about an existing company. The best choice depends on your team's expertise, location, available capital, and evidence of customer demand.";
        evidence = ImmutableArray.Create(ToEvidence(
            sources.Distinct().ToImmutableArray(),
            LatticeEvidenceKind.Derived));
        return true;

        bool TryGetBusinessIdeaDetail(
            string subject,
            string relation,
            out string label,
            out SemanticWorldModelSource source)
        {
            label = string.Empty;
            source = new SemanticWorldModelSource(string.Empty);
            var assertions = reasoner.GetRelations(subject, relation);
            if (assertions.Length != 1)
            {
                return false;
            }

            var concept = model.FindConcept(assertions[0].Object, SemanticConceptKind.Individual);
            if (concept is null)
            {
                return false;
            }

            label = concept.Label;
            source = assertions[0].Source;
            return true;
        }
    }

    internal static bool TryBeginnerGuidance(
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
}
