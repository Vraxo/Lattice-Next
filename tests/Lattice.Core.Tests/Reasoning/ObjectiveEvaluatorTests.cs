using System.Collections.Immutable;
using Lattice.Core.Language;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Reasoning;

public sealed class ObjectiveEvaluatorTests
{
    private readonly LceParser parser = new();
    private readonly ObjectiveEvaluator evaluator = new();

    [Fact]
    public void Evaluates_budget_transport_and_preferences_without_collapsing_unknowns()
    {
        var objective = CreateDayOutObjective();
        var knowledgeBase = CreateCandidateKnowledgeBase();

        var result = evaluator.Evaluate(objective, knowledgeBase);

        Assert.Equal(3, result.Candidates.Length);

        var fernbank = Assert.Single(result.Candidates, candidate => candidate.Candidate.Value == "Fernbank");
        Assert.Equal(
            ObjectiveConditionAssessmentStatus.Satisfied,
            GetCondition(fernbank, "spending-limit").Status);
        Assert.Equal(
            ObjectiveConditionAssessmentStatus.Satisfied,
            GetCondition(fernbank, "transport").Status);
        Assert.Equal(
            ObjectiveConditionAssessmentStatus.Satisfied,
            GetCondition(fernbank, "preference").Status);
        Assert.False(fernbank.HasHardConstraintViolation);

        var riverside = Assert.Single(result.Candidates, candidate => candidate.Candidate.Value == "Riverside");
        Assert.Equal(
            ObjectiveConditionAssessmentStatus.Satisfied,
            GetCondition(riverside, "spending-limit").Status);
        Assert.Equal(
            ObjectiveConditionAssessmentStatus.PartiallySatisfied,
            GetCondition(riverside, "preference").Status);
        Assert.False(riverside.HasHardConstraintViolation);

        var hilltop = Assert.Single(result.Candidates, candidate => candidate.Candidate.Value == "Hilltop");
        Assert.Equal(
            ObjectiveConditionAssessmentStatus.Unknown,
            GetCondition(hilltop, "spending-limit").Status);
        Assert.True(hilltop.HasUnresolvedHardConstraint);
    }

    [Fact]
    public void Walking_only_revision_rejects_bus_candidates_and_keeps_walking_candidates()
    {
        var objective = CreateDayOutObjective();
        var update = parser.ParseText("Actually, I don't want to take a bus. Walking only.");
        Assert.True(update.IsSuccess, update.Error);

        var conditions = objective.Conditions.ToBuilder();

        foreach (var utterance in update.Utterances)
        {
            var formula = Assert.IsType<ObjectiveConditionUpdateFormula>(utterance.Meaning);

            foreach (var condition in formula.Conditions)
            {
                conditions.Add(condition);
            }
        }

        objective = objective with { Conditions = conditions.ToImmutable() };
        var result = evaluator.Evaluate(objective, CreateCandidateKnowledgeBase());

        var fernbank = Assert.Single(result.Candidates, candidate => candidate.Candidate.Value == "Fernbank");
        Assert.Equal(
            ObjectiveConditionAssessmentStatus.Violated,
            GetCondition(fernbank, "transport-bus").Status);
        Assert.Equal(
            ObjectiveConditionAssessmentStatus.Violated,
            GetCondition(fernbank, "transport-mode").Status);
        Assert.True(fernbank.HasHardConstraintViolation);

        var riverside = Assert.Single(result.Candidates, candidate => candidate.Candidate.Value == "Riverside");
        Assert.Equal(
            ObjectiveConditionAssessmentStatus.Satisfied,
            GetCondition(riverside, "transport-bus").Status);
        Assert.Equal(
            ObjectiveConditionAssessmentStatus.Satisfied,
            GetCondition(riverside, "transport-mode").Status);
        Assert.False(riverside.HasHardConstraintViolation);
    }

    [Fact]
    public void Evaluates_time_window_containment_without_inventing_a_match()
    {
        var objective = new ObjectiveFormula("visit within the available time")
        {
            Conditions = ImmutableArray.Create(new ObjectiveCondition(
                "time-window",
                ObjectiveConditionRole.HardConstraint,
                ObjectiveConditionOperator.Within,
                new ObjectiveTimeWindowValue(new TimeOnly(9, 0), new TimeOnly(18, 0)),
                "I can leave at 9:00 and be back by 18:00.")
            {
                TargetAttribute = "opening-window"
            })
        };
        var knowledgeBase = new KnowledgeBase();
        AddAttribute(
            knowledgeBase,
            "WideHours",
            "opening-window",
            new ObjectiveTimeWindowValue(new TimeOnly(8, 0), new TimeOnly(20, 0)));
        AddAttribute(
            knowledgeBase,
            "ShortHours",
            "opening-window",
            new ObjectiveTimeWindowValue(new TimeOnly(10, 0), new TimeOnly(17, 0)));

        var result = evaluator.Evaluate(objective, knowledgeBase);

        var wide = Assert.Single(result.Candidates, candidate => candidate.Candidate.Value == "WideHours");
        var shortHours = Assert.Single(result.Candidates, candidate => candidate.Candidate.Value == "ShortHours");
        Assert.Equal(
            ObjectiveConditionAssessmentStatus.Satisfied,
            GetCondition(wide, "time-window").Status);
        Assert.Equal(
            ObjectiveConditionAssessmentStatus.Violated,
            GetCondition(shortHours, "time-window").Status);
    }

    [Fact]
    public void Conflicting_attribute_values_are_reported_as_conflicting()
    {
        var objective = CreateDayOutObjective();
        var knowledgeBase = new KnowledgeBase();
        AddAttribute(knowledgeBase, "Fernbank", "total-cost", new ObjectiveMoneyValue(6m, "$"));
        AddAttribute(knowledgeBase, "Fernbank", "total-cost", new ObjectiveMoneyValue(8m, "$"));

        var result = evaluator.Evaluate(objective, knowledgeBase);
        var candidate = Assert.Single(result.Candidates);

        Assert.Equal(
            ObjectiveConditionAssessmentStatus.Conflicting,
            GetCondition(candidate, "spending-limit").Status);
        Assert.True(candidate.HasUnresolvedHardConstraint);
    }

    [Fact]
    public void Knowledge_attributes_are_read_only_and_merge_provenance_for_duplicate_values()
    {
        var knowledgeBase = new KnowledgeBase();
        var attributes = Assert.IsAssignableFrom<IList<KnowledgeAttribute>>(knowledgeBase.Attributes);

        Assert.True(attributes.IsReadOnly);

        Assert.True(knowledgeBase.TryAddAttribute(
            new KnowledgeAttribute(
                new SemanticIdentifier("Fernbank"),
                new SemanticIdentifier("total-cost"),
                new ObjectiveMoneyValue(6m, "$"),
                "Fernbank costs six dollars.",
                KnowledgeSourceKind.ImportedClaim,
                SourceId: "day-out"),
            out var firstError), firstError);

        Assert.True(knowledgeBase.TryAddAttribute(
            new KnowledgeAttribute(
                new SemanticIdentifier("Fernbank"),
                new SemanticIdentifier("total-cost"),
                new ObjectiveMoneyValue(6m, "$"),
                "The return fare and entry total six dollars.",
                KnowledgeSourceKind.ExternalObservation,
                SourceId: "cost-check"),
            out var secondError), secondError);

        var attribute = Assert.Single(knowledgeBase.Attributes);
        Assert.Equal(2, attribute.Sources.Length);
        Assert.Equal("day-out", attribute.Sources[0].SourceId);
        Assert.Equal("cost-check", attribute.Sources[1].SourceId);
    }

    [Fact]
    public void Rejects_invalid_typed_attribute_values()
    {
        var knowledgeBase = new KnowledgeBase();
        var attribute = new KnowledgeAttribute(
            new SemanticIdentifier("Fernbank"),
            new SemanticIdentifier("tags"),
            new ObjectiveTextSetValue(ImmutableArray<string>.Empty),
            "Fernbank has no supplied tags.");

        var added = knowledgeBase.TryAddAttribute(attribute, out var error);

        Assert.False(added);
        Assert.Contains("valid typed value", error);
        Assert.Empty(knowledgeBase.Attributes);
    }

    private ObjectiveFormula CreateDayOutObjective()
    {
        const string input = "I have a free Saturday, a limited budget, and no car. I would prefer somewhere quiet and outdoors. Help me put together a good day.";
        var parsed = parser.ParseText(input);
        Assert.True(parsed.IsSuccess, parsed.Error);
        var objective = Assert.IsType<ObjectiveFormula>(Assert.Single(parsed.Utterances).Meaning);

        return objective with
        {
            Conditions = objective.Conditions
                .Select(condition => condition.Name == "spending-limit"
                    ? condition with { Value = new ObjectiveMoneyValue(25m, "$") }
                    : condition)
                .ToImmutableArray()
        };
    }

    private static KnowledgeBase CreateCandidateKnowledgeBase()
    {
        var knowledgeBase = new KnowledgeBase();

        AddAttribute(knowledgeBase, "Fernbank", "total-cost", new ObjectiveMoneyValue(6m, "$"));
        AddAttribute(knowledgeBase, "Fernbank", "transport-mode", new ObjectiveTextValue("bus"));
        AddAttribute(knowledgeBase, "Fernbank", "tags", new ObjectiveTextSetValue(
            ImmutableArray.Create("quiet", "outdoors")));

        AddAttribute(knowledgeBase, "Riverside", "total-cost", new ObjectiveMoneyValue(0m, "$"));
        AddAttribute(knowledgeBase, "Riverside", "transport-mode", new ObjectiveTextValue("walking"));
        AddAttribute(knowledgeBase, "Riverside", "tags", new ObjectiveTextSetValue(
            ImmutableArray.Create("busy", "outdoors")));

        AddAttribute(knowledgeBase, "Hilltop", "transport-mode", new ObjectiveTextValue("bus"));
        AddAttribute(knowledgeBase, "Hilltop", "tags", new ObjectiveTextSetValue(
            ImmutableArray.Create("quiet", "outdoors")));

        return knowledgeBase;
    }

    private static void AddAttribute(
        KnowledgeBase knowledgeBase,
        string candidate,
        string name,
        ObjectiveConditionValue value)
    {
        var added = knowledgeBase.TryAddAttribute(
            new KnowledgeAttribute(
                new SemanticIdentifier(candidate),
                new SemanticIdentifier(name),
                value,
                $"{candidate} {name} is represented by {value}.",
                KnowledgeSourceKind.ImportedClaim,
                SourceId: "candidate-fixture"),
            out var error);

        Assert.True(added, error);
    }

    private static ObjectiveConditionAssessment GetCondition(
        CandidateAssessment candidate,
        string name) =>
        Assert.Single(candidate.Conditions, condition => condition.ConditionName == name);
}
