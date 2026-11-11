using System.Collections.Immutable;
using Lattice.Core.Language;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Reasoning;

public sealed class DayOutBenchmarkDataTests
{
    [Fact]
    public void Loads_the_complete_day_out_dataset_and_evaluates_supported_objective_conditions()
    {
        var json = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "day-out-candidates.json"));
        var knowledgeBase = new KnowledgeBase();
        var loader = new KnowledgePackLoader();

        var loaded = loader.TryLoadAndAdd(json, knowledgeBase, out var pack, out var error);

        Assert.True(loaded, error);
        Assert.Equal("day-out-candidates", pack!.Id);
        Assert.Equal(42, pack.AttributeCount);
        Assert.Equal(42, knowledgeBase.Attributes.Count);

        var candidates = knowledgeBase.Attributes
            .Select(attribute => attribute.Subject.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(4, candidates.Length);
        Assert.Contains("Fernbank Nature Reserve", candidates);
        Assert.Contains("Quarry Lake Trail", candidates);
        Assert.Contains("Riverside Gardens", candidates);
        Assert.Contains("Hilltop Farm Grounds", candidates);

        var parser = new LceParser();
        var parsed = parser.ParseText(
            "I have a free Saturday, a limited budget, and no car. " +
            "I would prefer somewhere quiet and outdoors. " +
            "Help me put together a good day.");
        Assert.True(parsed.IsSuccess, parsed.Error);
        var objective = Assert.IsType<ObjectiveFormula>(Assert.Single(parsed.Utterances).Meaning);
        objective = objective with
        {
            Conditions = objective.Conditions
                .Select(condition => condition.Name == "spending-limit"
                    ? condition with { Value = new ObjectiveMoneyValue(25m, "$") }
                    : condition)
                .Append(new ObjectiveCondition(
                    "opening-hours",
                    ObjectiveConditionRole.HardConstraint,
                    ObjectiveConditionOperator.Within,
                    new ObjectiveTimeWindowValue(new TimeOnly(9, 0), new TimeOnly(18, 0)),
                    "I can leave at 9:00 and be back by 18:00.")
                {
                    TargetAttribute = "opening-window"
                })
                .ToImmutableArray()
        };

        var evaluation = new ObjectiveEvaluator().Evaluate(objective, knowledgeBase);

        Assert.Equal(4, evaluation.Candidates.Length);

        var fernbank = GetCandidate(evaluation, "Fernbank Nature Reserve");
        Assert.Equal(ObjectiveConditionAssessmentStatus.Satisfied, GetCondition(fernbank, "spending-limit").Status);
        Assert.Equal(ObjectiveConditionAssessmentStatus.Satisfied, GetCondition(fernbank, "transport").Status);
        Assert.Equal(ObjectiveConditionAssessmentStatus.Satisfied, GetCondition(fernbank, "preference").Status);
        Assert.Equal(ObjectiveConditionAssessmentStatus.Satisfied, GetCondition(fernbank, "opening-hours").Status);
        Assert.Equal("day-out-candidates", GetCondition(fernbank, "spending-limit").Evidence[0].SourceId);

        var quarry = GetCandidate(evaluation, "Quarry Lake Trail");
        Assert.Equal(ObjectiveConditionAssessmentStatus.Satisfied, GetCondition(quarry, "spending-limit").Status);
        Assert.Equal(ObjectiveConditionAssessmentStatus.Satisfied, GetCondition(quarry, "preference").Status);
        Assert.Equal(ObjectiveConditionAssessmentStatus.Violated, GetCondition(quarry, "opening-hours").Status);

        var riverside = GetCandidate(evaluation, "Riverside Gardens");
        Assert.Equal(ObjectiveConditionAssessmentStatus.Satisfied, GetCondition(riverside, "spending-limit").Status);
        Assert.Equal(ObjectiveConditionAssessmentStatus.Satisfied, GetCondition(riverside, "transport").Status);
        Assert.Equal(ObjectiveConditionAssessmentStatus.PartiallySatisfied, GetCondition(riverside, "preference").Status);
        Assert.Equal(ObjectiveConditionAssessmentStatus.Satisfied, GetCondition(riverside, "opening-hours").Status);

        var hilltop = GetCandidate(evaluation, "Hilltop Farm Grounds");
        Assert.Equal(ObjectiveConditionAssessmentStatus.Satisfied, GetCondition(hilltop, "spending-limit").Status);
        Assert.Equal(ObjectiveConditionAssessmentStatus.Satisfied, GetCondition(hilltop, "preference").Status);
        Assert.Equal(ObjectiveConditionAssessmentStatus.Violated, GetCondition(hilltop, "opening-hours").Status);
    }

    [Fact]
    public void Preserves_the_published_timetables_and_leaves_unspecified_facilities_absent()
    {
        var json = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "day-out-candidates.json"));
        var knowledgeBase = new KnowledgeBase();
        Assert.True(new KnowledgePackLoader().TryLoadAndAdd(
            json,
            knowledgeBase,
            out _,
            out var error), error);

        Assert.Equal(
            new[] { new TimeOnly(9, 0), new TimeOnly(10, 30), new TimeOnly(12, 0) },
            GetTimeSet(knowledgeBase, "Fernbank Nature Reserve", "outbound-bus-departures").Times);
        Assert.Equal(
            new[] { new TimeOnly(14, 0), new TimeOnly(15, 30), new TimeOnly(17, 0) },
            GetTimeSet(knowledgeBase, "Fernbank Nature Reserve", "return-bus-departures").Times);
        Assert.Equal(
            new[] { new TimeOnly(15, 45) },
            GetTimeSet(knowledgeBase, "Quarry Lake Trail", "return-bus-departures").Times);
        Assert.Equal(
            new[] { new TimeOnly(15, 0), new TimeOnly(16, 30) },
            GetTimeSet(knowledgeBase, "Hilltop Farm Grounds", "return-bus-departures").Times);

        Assert.DoesNotContain(knowledgeBase.Attributes, attribute =>
            attribute.Subject.Value == "Quarry Lake Trail" &&
            attribute.Name.Value.Contains("facility", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(knowledgeBase.Attributes, attribute =>
            attribute.Subject.Value == "Hilltop Farm Grounds" &&
            attribute.Name.Value == "food-service");
        Assert.DoesNotContain(knowledgeBase.Attributes, attribute =>
            attribute.Subject.Value == "Riverside Gardens" &&
            attribute.Name.Value.Contains("bus-departure", StringComparison.OrdinalIgnoreCase));
    }

    private static CandidateAssessment GetCandidate(
        ObjectiveEvaluation evaluation,
        string name) =>
        Assert.Single(evaluation.Candidates, candidate => candidate.Candidate.Value == name);

    private static ObjectiveConditionAssessment GetCondition(
        CandidateAssessment candidate,
        string name) =>
        Assert.Single(candidate.Conditions, condition => condition.ConditionName == name);

    private static ObjectiveTimeSetValue GetTimeSet(
        KnowledgeBase knowledgeBase,
        string candidate,
        string attribute) =>
        Assert.IsType<ObjectiveTimeSetValue>(Assert.Single(
            knowledgeBase.Attributes,
            item => item.Subject.Value == candidate && item.Name.Value == attribute).Value);
}
