using Lattice.Client;
using Lattice.Runtime;
using Xunit;

namespace Lattice.Runtime.Tests;

public sealed class ObjectiveSessionTests
{
    [Fact]
    public async Task Exposes_evidence_based_candidate_assessments_in_session_status()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);
        const string pack = """
            {
              "formatVersion": 1,
              "id": "objective-evaluation-test",
              "name": "Objective evaluation test",
              "claims": [],
              "attributes": [
                { "subject": "Quiet Park", "name": "total-cost", "value": { "type": "money", "amount": 10, "currencySymbol": "$" }, "sourceText": "Quiet Park costs $10." },
                { "subject": "Quiet Park", "name": "transport-mode", "value": { "type": "text", "value": "walking" }, "sourceText": "Quiet Park is reachable by walking." },
                { "subject": "Quiet Park", "name": "tags", "value": { "type": "textSet", "values": ["quiet", "outdoors"] }, "sourceText": "Quiet Park is quiet and outdoors." },
                { "subject": "Busy Park", "name": "total-cost", "value": { "type": "money", "amount": 10, "currencySymbol": "$" }, "sourceText": "Busy Park costs $10." },
                { "subject": "Busy Park", "name": "transport-mode", "value": { "type": "text", "value": "car" }, "sourceText": "Busy Park requires a car." },
                { "subject": "Busy Park", "name": "tags", "value": { "type": "textSet", "values": ["busy", "outdoors"] }, "sourceText": "Busy Park is busy and outdoors." }
              ]
            }
            """;

        var loaded = await session.LoadKnowledgePackAsync(pack, TestContext.Current.CancellationToken);
        Assert.True(loaded.IsSuccess, loaded.Error);

        await session.SubmitAsync(
            "I have a limited budget and no car. I would prefer somewhere quiet and outdoors. Help me choose a place. My budget is $25.",
            TestContext.Current.CancellationToken);

        var status = await session.GetStatusAsync(TestContext.Current.CancellationToken);
        var evaluation = Assert.IsType<LatticeObjectiveEvaluation>(status.ObjectiveEvaluation);
        Assert.Equal(2, evaluation.Candidates.Length);

        var quiet = Assert.Single(evaluation.Candidates, candidate => candidate.Candidate == "Quiet Park");
        Assert.False(quiet.HasHardConstraintViolation);
        Assert.False(quiet.HasUnresolvedHardConstraint);
        Assert.All(quiet.Conditions, condition => Assert.Equal("Satisfied", condition.Status));
        Assert.Contains(
            quiet.Conditions.SelectMany(condition => condition.Evidence),
            source => source.SourceText == "Quiet Park is quiet and outdoors.");

        var busy = Assert.Single(evaluation.Candidates, candidate => candidate.Candidate == "Busy Park");
        Assert.True(busy.HasHardConstraintViolation);
        Assert.False(busy.HasUnresolvedHardConstraint);
        Assert.Contains(busy.Conditions, condition => condition.Name == "preference" && condition.Status == "PartiallySatisfied");
    }

    [Theory]
    [InlineData("I have a free Saturday, a limited budget, and no car. I would prefer somewhere quiet and outdoors. Help me put together a good day.", "put together a good day", LatticeUtteranceResultKind.ClarificationRequired)]
    [InlineData("I'm looking for a laptop that will last a long time. I don't need gaming performance, but I care about screen quality, reliability, and repairability. Help me compare these three options. My budget is $1,000.", "compare these three options", LatticeUtteranceResultKind.ObjectiveReceived)]
    public async Task Retains_objective_and_original_request_without_adding_it_to_knowledge(
        string input,
        string expectedGoal,
        LatticeUtteranceResultKind expectedKind)
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(input, TestContext.Current.CancellationToken);
        var status = await session.GetStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(expectedKind, Assert.Single(result.Utterances).Kind);
        Assert.Equal(expectedGoal, status.ActiveObjective);
        Assert.Equal(input, status.ActiveObjectiveSource);
        Assert.Equal(0, status.FactCount);
        Assert.Equal(0, status.RuleCount);

        if (expectedKind == LatticeUtteranceResultKind.ClarificationRequired)
        {
            Assert.Equal("spending-limit", status.PendingInformationName);
            Assert.Equal("What is your spending limit?", status.PendingInformationPrompt);
        }
        else
        {
            Assert.Null(status.PendingInformationName);
            Assert.Null(status.PendingInformationPrompt);
        }
    }

    [Fact]
    public async Task Laptop_objective_uses_the_same_generic_condition_model_without_a_special_workflow()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);
        const string input = "I'm looking for a laptop that will last a long time. I don't need gaming performance, but I care about screen quality, reliability, and repairability. Help me compare these three options. My budget is $1,000.";

        var result = await session.SubmitAsync(input, TestContext.Current.CancellationToken);
        var status = await session.GetStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.ObjectiveReceived, utterance.Kind);
        Assert.Contains("hardconstraint(spending-limit, atmost, $1000)", utterance.Meaning);
        Assert.Contains("preference(preference, desires, screen quality+reliability+repairability)", utterance.Meaning);
        Assert.Equal("compare these three options", status.ActiveObjective);
        Assert.Equal(input, status.ActiveObjectiveSource);
        Assert.Null(status.PendingInformationName);
        Assert.Equal(0, status.FactCount);
        Assert.Equal(0, status.RuleCount);
    }

    [Fact]
    public async Task Applies_a_typed_answer_to_the_pending_condition_without_storing_a_fact()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        await session.SubmitAsync(
            "I have a free Saturday, a limited budget, and no car. I would prefer somewhere quiet and outdoors. Help me put together a good day.",
            TestContext.Current.CancellationToken);

        var answer = await session.SubmitAsync("$25 total.", TestContext.Current.CancellationToken);
        var status = await session.GetStatusAsync(TestContext.Current.CancellationToken);

        var utterance = Assert.Single(answer.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.ObjectiveInformationReceived, utterance.Kind);
        Assert.Contains("Recorded $25 total. for spending-limit.", utterance.Message);
        Assert.Contains("hardconstraint(spending-limit, atmost, $25)", utterance.Meaning);
        Assert.Null(status.PendingInformationName);
        Assert.Null(status.PendingInformationPrompt);
        Assert.Contains("$25 total.", status.ActiveObjectiveSource);
        Assert.Equal(0, status.FactCount);
        Assert.Equal(0, status.RuleCount);
    }

    [Fact]
    public async Task Unsupported_sentence_after_valid_follow_up_leaves_the_session_unchanged()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);
        const string originalRequest = "I have a free Saturday, a limited budget, and no car. I would prefer somewhere quiet and outdoors. Help me put together a good day.";

        await session.SubmitAsync(originalRequest, TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(
            "$25 total. I'll bring lunch. I'm starting at Central Station, and I can leave at 9:00 and be back by 18:00. Maybe.",
            TestContext.Current.CancellationToken);
        var status = await session.GetStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal(LatticeTurnStatus.Rejected, result.Status);
        Assert.Contains("Sentence 4:", result.Error);
        Assert.Equal("put together a good day", status.ActiveObjective);
        Assert.Equal(originalRequest, status.ActiveObjectiveSource);
        Assert.Equal("spending-limit", status.PendingInformationName);
        Assert.Equal(0, status.FactCount);
    }

    [Fact]
    public async Task Applies_the_complete_day_out_follow_up_without_storing_facts()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);
        const string originalRequest = "I have a free Saturday, a limited budget, and no car. I would prefer somewhere quiet and outdoors. Help me put together a good day.";
        const string followUp = "$25 total. I'll bring lunch. I'm starting at Central Station, and I can leave at 9:00 and be back by 18:00.";

        await session.SubmitAsync(originalRequest, TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(followUp, TestContext.Current.CancellationToken);
        var status = await session.GetStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(3, result.Utterances.Length);
        Assert.All(
            result.Utterances,
            utterance => Assert.Equal(
                LatticeUtteranceResultKind.ObjectiveInformationReceived,
                utterance.Kind));

        var meaning = result.Utterances[^1].Meaning;
        Assert.Contains("hardconstraint(spending-limit, atmost, $25)", meaning);
        Assert.Contains("hardconstraint(meal-plan, equals, bring-lunch)", meaning);
        Assert.Contains("hardconstraint(origin, equals, Central Station)", meaning);
        Assert.Contains("hardconstraint(time-window, within, 09:00-18:00)", meaning);
        Assert.Null(status.PendingInformationName);
        Assert.Null(status.PendingInformationPrompt);
        Assert.Contains("$25 total.", status.ActiveObjectiveSource);
        Assert.Contains("Central Station", status.ActiveObjectiveSource);
        Assert.Equal(0, status.FactCount);
        Assert.Equal(0, status.RuleCount);
    }

    [Fact]
    public async Task Walking_only_revision_replaces_transport_mode_and_preserves_other_conditions()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);
        const string originalRequest = "I have a free Saturday, a limited budget, and no car. I would prefer somewhere quiet and outdoors. Help me put together a good day.";

        await session.SubmitAsync(originalRequest, TestContext.Current.CancellationToken);
        await session.SubmitAsync(
            "$25 total. I'll bring lunch. I'm starting at Central Station, and I can leave at 9:00 and be back by 18:00.",
            TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(
            "Actually, I don't want to take a bus. Walking only.",
            TestContext.Current.CancellationToken);
        var status = await session.GetStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(2, result.Utterances.Length);
        Assert.All(
            result.Utterances,
            utterance => Assert.Equal(
                LatticeUtteranceResultKind.ObjectiveInformationReceived,
                utterance.Kind));

        var meaning = result.Utterances[^1].Meaning;
        Assert.Contains("hardconstraint(transport, excludes, car)", meaning);
        Assert.Contains("hardconstraint(transport-bus, excludes, bus)", meaning);
        Assert.Contains("hardconstraint(transport-mode, equals, walking)", meaning);
        Assert.Contains("hardconstraint(spending-limit, atmost, $25)", meaning);
        Assert.Contains("hardconstraint(origin, equals, Central Station)", meaning);
        Assert.Contains("hardconstraint(time-window, within, 09:00-18:00)", meaning);
        Assert.Contains("bring-lunch", meaning);
        Assert.Contains("Walking only.", status.ActiveObjectiveSource);
        Assert.Equal(0, status.FactCount);
        Assert.Equal(0, status.RuleCount);
    }

    [Fact]
    public async Task Evaluates_candidate_temporal_plans_from_the_user_time_window_and_revises_for_walking_only()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);
        const string pack = """
            {
              "formatVersion": 1,
              "id": "temporal-session-test",
              "name": "Temporal session test",
              "claims": [],
              "attributes": [
                { "subject": "Bus Park", "name": "total-cost", "value": { "type": "money", "amount": 6, "currencySymbol": "$" }, "sourceText": "Bus Park costs $6." },
                { "subject": "Bus Park", "name": "transport-mode", "value": { "type": "text", "value": "bus" }, "sourceText": "Bus Park is reached by bus." },
                { "subject": "Bus Park", "name": "tags", "value": { "type": "textSet", "values": ["quiet", "outdoors"] }, "sourceText": "Bus Park is quiet and outdoors." },
                { "subject": "Bus Park", "name": "one-way-travel-duration", "value": { "type": "duration", "minutes": 25 }, "sourceText": "The bus journey takes 25 minutes each way." },
                { "subject": "Bus Park", "name": "activity-duration", "value": { "type": "duration", "minutes": 120 }, "sourceText": "The activity takes 2 hours." },
                { "subject": "Bus Park", "name": "opening-window", "value": { "type": "timeWindow", "start": "08:00", "end": "18:00" }, "sourceText": "Bus Park is open from 08:00 to 18:00." },
                { "subject": "Bus Park", "name": "outbound-bus-departures", "value": { "type": "timeSet", "times": ["09:00", "10:30"] }, "sourceText": "Buses depart at 09:00 and 10:30." },
                { "subject": "Bus Park", "name": "return-bus-departures", "value": { "type": "timeSet", "times": ["14:00", "16:00"] }, "sourceText": "Return buses depart at 14:00 and 16:00." },
                { "subject": "Bus Park", "name": "temporal-plan", "value": { "type": "temporalPlan", "stages": [
                  { "durationAttribute": "one-way-travel-duration", "startTimesAttribute": "outbound-bus-departures" },
                  { "durationAttribute": "activity-duration", "availabilityWindowAttribute": "opening-window" },
                  { "durationAttribute": "one-way-travel-duration", "startTimesAttribute": "return-bus-departures" }
                ] }, "sourceText": "The outing is an outbound scheduled journey, an activity, and a return scheduled journey." },
                { "subject": "Walk Park", "name": "total-cost", "value": { "type": "money", "amount": 0, "currencySymbol": "$" }, "sourceText": "Walk Park is free." },
                { "subject": "Walk Park", "name": "transport-mode", "value": { "type": "text", "value": "walking" }, "sourceText": "Walk Park is reachable on foot." },
                { "subject": "Walk Park", "name": "tags", "value": { "type": "textSet", "values": ["busy", "outdoors"] }, "sourceText": "Walk Park is busy and outdoors." },
                { "subject": "Walk Park", "name": "one-way-travel-duration", "value": { "type": "duration", "minutes": 15 }, "sourceText": "The walk takes 15 minutes each way." },
                { "subject": "Walk Park", "name": "activity-duration", "value": { "type": "duration", "minutes": 90 }, "sourceText": "The activity takes 90 minutes." },
                { "subject": "Walk Park", "name": "opening-window", "value": { "type": "timeWindow", "start": "06:00", "end": "20:00" }, "sourceText": "Walk Park is open from 06:00 to 20:00." },
                { "subject": "Walk Park", "name": "temporal-plan", "value": { "type": "temporalPlan", "stages": [
                  { "durationAttribute": "one-way-travel-duration" },
                  { "durationAttribute": "activity-duration", "availabilityWindowAttribute": "opening-window" },
                  { "durationAttribute": "one-way-travel-duration" }
                ] }, "sourceText": "The outing is a walk to the park, an activity, and a return walk." }
              ]
            }
            """;
        var loaded = await session.LoadKnowledgePackAsync(pack, TestContext.Current.CancellationToken);
        Assert.True(loaded.IsSuccess, loaded.Error);

        await session.SubmitAsync(
            "I have a free Saturday, a limited budget, and no car. I would prefer somewhere quiet and outdoors. Help me put together a good day.",
            TestContext.Current.CancellationToken);
        var followUp = await session.SubmitAsync(
            "$25 total. I'll bring lunch. I'm starting at Central Station, and I can leave at 9:00 and be back by 18:00.",
            TestContext.Current.CancellationToken);
        Assert.Equal(LatticeTurnStatus.Completed, followUp.Status);

        var beforeWalkingOnly = await session.GetStatusAsync(TestContext.Current.CancellationToken);
        var initialEvaluation = Assert.IsType<LatticeObjectiveEvaluation>(beforeWalkingOnly.ObjectiveEvaluation);
        var busPark = Assert.Single(initialEvaluation.Candidates, candidate => candidate.Candidate == "Bus Park");
        var walkPark = Assert.Single(initialEvaluation.Candidates, candidate => candidate.Candidate == "Walk Park");
        Assert.Contains(busPark.Conditions, condition => condition.Name == "time-window" && condition.Status == "Satisfied");
        Assert.Contains(walkPark.Conditions, condition => condition.Name == "time-window" && condition.Status == "Satisfied");

        var walkingOnly = await session.SubmitAsync(
            "Actually, I don't want to take a bus. Walking only.",
            TestContext.Current.CancellationToken);
        Assert.Equal(LatticeTurnStatus.Completed, walkingOnly.Status);

        var afterWalkingOnly = await session.GetStatusAsync(TestContext.Current.CancellationToken);
        var revisedEvaluation = Assert.IsType<LatticeObjectiveEvaluation>(afterWalkingOnly.ObjectiveEvaluation);
        busPark = Assert.Single(revisedEvaluation.Candidates, candidate => candidate.Candidate == "Bus Park");
        walkPark = Assert.Single(revisedEvaluation.Candidates, candidate => candidate.Candidate == "Walk Park");
        Assert.True(busPark.HasHardConstraintViolation);
        Assert.False(walkPark.HasHardConstraintViolation);
        Assert.Contains(walkPark.Conditions, condition => condition.Name == "time-window" && condition.Status == "Satisfied");
        Assert.Contains(afterWalkingOnly.ActiveObjectiveSource, "Walking only.");
    }

    [Fact]
    public async Task Unrelated_assertions_do_not_consume_a_pending_information_request()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        await session.SubmitAsync(
            "I have a free Saturday and a limited budget. Help me put together a good day.",
            TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync("Fido is a dog.", TestContext.Current.CancellationToken);
        var status = await session.GetStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal(LatticeUtteranceResultKind.StatementStored, Assert.Single(result.Utterances).Kind);
        Assert.Equal("spending-limit", status.PendingInformationName);
        Assert.Equal("What is your spending limit?", status.PendingInformationPrompt);
        Assert.Equal(1, status.FactCount);
    }

    [Fact]
    public async Task Money_answers_without_a_pending_request_are_not_stored_as_facts()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync("$25 total.", TestContext.Current.CancellationToken);
        var status = await session.GetStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal(LatticeUtteranceResultKind.ClarificationRequired, Assert.Single(result.Utterances).Kind);
        Assert.Contains("no outstanding objective information request", result.Utterances[0].Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, status.FactCount);
        Assert.Null(status.ActiveObjective);
    }

    [Fact]
    public async Task Retains_objective_when_later_turns_add_knowledge()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        await session.SubmitAsync("I have a free Saturday. Help me put together a good day.", TestContext.Current.CancellationToken);
        await session.SubmitAsync("Fido is a dog.", TestContext.Current.CancellationToken);
        var status = await session.GetStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal("put together a good day", status.ActiveObjective);
        Assert.Contains("I have a free Saturday.", status.ActiveObjectiveSource);
        Assert.Equal(1, status.FactCount);
    }
}
