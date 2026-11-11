using System.Collections.Immutable;
using Lattice.Core.Language;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Reasoning;

public sealed class DayOutBenchmarkDataTests
{
    [Fact]
    public void Loads_the_complete_day_out_dataset_and_evaluates_objective_conditions()
    {
        var knowledgeBase = LoadCandidates();
        Assert.Equal(50, knowledgeBase.Attributes.Count);

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
                .Append(CreateTemporalPlanCondition(new TimeOnly(9, 0), new TimeOnly(18, 0)))
                .ToImmutableArray()
        };

        var evaluation = new ObjectiveEvaluator().Evaluate(objective, knowledgeBase);
        Assert.Equal(4, evaluation.Candidates.Length);

        var fernbank = GetCandidate(evaluation, "Fernbank Nature Reserve");
        Assert.Equal(ObjectiveConditionAssessmentStatus.Satisfied, GetCondition(fernbank, "spending-limit").Status);
        Assert.Contains(
            GetCondition(fernbank, "spending-limit").Evidence,
            attribute => attribute.Name.Value == "return-transport-cost");
        Assert.Contains(
            GetCondition(fernbank, "spending-limit").Evidence,
            attribute => attribute.Name.Value == "entry-cost");
        Assert.Equal(ObjectiveConditionAssessmentStatus.Satisfied, GetCondition(fernbank, "transport").Status);
        Assert.Equal(ObjectiveConditionAssessmentStatus.Satisfied, GetCondition(fernbank, "preference").Status);
        Assert.Equal(ObjectiveConditionAssessmentStatus.Satisfied, GetCondition(fernbank, "itinerary").Status);
        Assert.Equal("day-out-candidates", GetCondition(fernbank, "itinerary").Evidence[0].SourceId);

        var quarry = GetCandidate(evaluation, "Quarry Lake Trail");
        Assert.Equal(ObjectiveConditionAssessmentStatus.Satisfied, GetCondition(quarry, "spending-limit").Status);
        Assert.Equal(ObjectiveConditionAssessmentStatus.Satisfied, GetCondition(quarry, "preference").Status);
        Assert.Equal(ObjectiveConditionAssessmentStatus.Satisfied, GetCondition(quarry, "itinerary").Status);

        var riverside = GetCandidate(evaluation, "Riverside Gardens");
        Assert.Equal(ObjectiveConditionAssessmentStatus.Satisfied, GetCondition(riverside, "spending-limit").Status);
        Assert.Equal(ObjectiveConditionAssessmentStatus.Satisfied, GetCondition(riverside, "transport").Status);
        Assert.Equal(ObjectiveConditionAssessmentStatus.PartiallySatisfied, GetCondition(riverside, "preference").Status);
        Assert.Equal(ObjectiveConditionAssessmentStatus.Satisfied, GetCondition(riverside, "itinerary").Status);

        var hilltop = GetCandidate(evaluation, "Hilltop Farm Grounds");
        Assert.Equal(ObjectiveConditionAssessmentStatus.Satisfied, GetCondition(hilltop, "spending-limit").Status);
        Assert.Equal(ObjectiveConditionAssessmentStatus.Satisfied, GetCondition(hilltop, "preference").Status);
        Assert.Equal(ObjectiveConditionAssessmentStatus.Satisfied, GetCondition(hilltop, "itinerary").Status);
    }

    [Fact]
    public void Uses_the_actual_activity_interval_instead_of_requiring_opening_hours_to_cover_the_whole_day()
    {
        var objective = new ObjectiveFormula("fit an outing into the available time")
        {
            Conditions = ImmutableArray.Create(
                CreateTemporalPlanCondition(new TimeOnly(9, 0), new TimeOnly(18, 0)))
        };

        var result = new ObjectiveEvaluator().Evaluate(objective, LoadCandidates());

        Assert.Equal(ObjectiveConditionAssessmentStatus.Satisfied,
            GetCondition(GetCandidate(result, "Quarry Lake Trail"), "itinerary").Status);
        Assert.Equal(ObjectiveConditionAssessmentStatus.Satisfied,
            GetCondition(GetCandidate(result, "Hilltop Farm Grounds"), "itinerary").Status);
    }

    [Fact]
    public void Rejects_a_timetable_when_the_only_return_service_misses_the_deadline()
    {
        var objective = new ObjectiveFormula("return by 16:00")
        {
            Conditions = ImmutableArray.Create(
                CreateTemporalPlanCondition(new TimeOnly(9, 0), new TimeOnly(16, 0)))
        };

        var result = new ObjectiveEvaluator().Evaluate(objective, LoadCandidates());

        Assert.Equal(ObjectiveConditionAssessmentStatus.Satisfied,
            GetCondition(GetCandidate(result, "Fernbank Nature Reserve"), "itinerary").Status);
        Assert.Equal(ObjectiveConditionAssessmentStatus.Violated,
            GetCondition(GetCandidate(result, "Quarry Lake Trail"), "itinerary").Status);
        Assert.Equal(ObjectiveConditionAssessmentStatus.Satisfied,
            GetCondition(GetCandidate(result, "Hilltop Farm Grounds"), "itinerary").Status);
    }

    [Fact]
    public void Checks_a_walking_sequence_without_assuming_a_published_timetable()
    {
        var walkingSequence = new ObjectiveTemporalSequenceValue(
            new TimeOnly(9, 0),
            new TimeOnly(18, 0),
            ImmutableArray.Create(
                new ObjectiveTemporalStage("one-way-travel-duration"),
                new ObjectiveTemporalStage(
                    "activity-duration",
                    AvailabilityWindowAttribute: "opening-window"),
                new ObjectiveTemporalStage("one-way-travel-duration")));
        var objective = new ObjectiveFormula("fit a walking outing")
        {
            Conditions = ImmutableArray.Create(new ObjectiveCondition(
                "walking-itinerary",
                ObjectiveConditionRole.HardConstraint,
                ObjectiveConditionOperator.CanFit,
                walkingSequence,
                "A walking outing must fit between 09:00 and 18:00."))
        };

        var result = new ObjectiveEvaluator().Evaluate(objective, LoadCandidates());

        Assert.Equal(ObjectiveConditionAssessmentStatus.Satisfied,
            GetCondition(GetCandidate(result, "Riverside Gardens"), "walking-itinerary").Status);
    }

    [Fact]
    public void Reports_conflicting_temporal_attributes_instead_of_selecting_one()
    {
        var knowledgeBase = LoadCandidates();
        var added = knowledgeBase.TryAddAttribute(
            new KnowledgeAttribute(
                new SemanticIdentifier("Fernbank Nature Reserve"),
                new SemanticIdentifier("one-way-travel-duration"),
                new ObjectiveDurationValue(40),
                "A conflicting observation says the journey takes 40 minutes."),
            out var error);
        Assert.True(added, error);

        var objective = new ObjectiveFormula("fit an outing")
        {
            Conditions = ImmutableArray.Create(
                CreateTemporalPlanCondition(new TimeOnly(9, 0), new TimeOnly(18, 0)))
        };

        var result = new ObjectiveEvaluator().Evaluate(objective, knowledgeBase);

        Assert.Equal(
            ObjectiveConditionAssessmentStatus.Conflicting,
            GetCondition(GetCandidate(result, "Fernbank Nature Reserve"), "itinerary").Status);
    }

    [Fact]
    public void Preserves_published_timetables_and_leaves_unspecified_facilities_absent()
    {
        var knowledgeBase = LoadCandidates();

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

    private static KnowledgeBase LoadCandidates()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "day-out-candidates.json"));
        var knowledgeBase = new KnowledgeBase();
        Assert.True(new KnowledgePackLoader().TryLoadAndAdd(json, knowledgeBase, out _, out var error), error);
        return knowledgeBase;
    }

    private static ObjectiveCondition CreateTemporalPlanCondition(TimeOnly earliest, TimeOnly latest) =>
        new(
            "itinerary",
            ObjectiveConditionRole.HardConstraint,
            ObjectiveConditionOperator.CanFit,
            new ObjectiveTimeWindowValue(earliest, latest),
            $"The outing must fit between {earliest} and {latest}.")
        {
            TargetAttribute = "temporal-plan"
        };

    private static CandidateAssessment GetCandidate(ObjectiveEvaluation evaluation, string name) =>
        Assert.Single(evaluation.Candidates, candidate => candidate.Candidate.Value == name);

    private static ObjectiveConditionAssessment GetCondition(CandidateAssessment candidate, string name) =>
        Assert.Single(candidate.Conditions, condition => condition.ConditionName == name);

    private static ObjectiveTimeSetValue GetTimeSet(KnowledgeBase knowledgeBase, string candidate, string attribute) =>
        Assert.IsType<ObjectiveTimeSetValue>(Assert.Single(
            knowledgeBase.Attributes,
            item => item.Subject.Value == candidate && item.Name.Value == attribute).Value);
}
