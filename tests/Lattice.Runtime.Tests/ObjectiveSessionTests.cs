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
                { "subject": "Busy Park", "name": "tags", "value": { "type": "textSet", "values": ["busy", "outdoors"] }, "sourceText": "Busy Park is busy and outdoors." },
                { "subject": "Noisy Park", "name": "total-cost", "value": { "type": "money", "amount": 12, "currencySymbol": "$" }, "sourceText": "Noisy Park costs $12." },
                { "subject": "Noisy Park", "name": "transport-mode", "value": { "type": "text", "value": "walking" }, "sourceText": "Noisy Park is reachable by walking." },
                { "subject": "Noisy Park", "name": "tags", "value": { "type": "textSet", "values": ["outdoors"], "excludedValues": ["quiet"] }, "sourceText": "Noisy Park is outdoors and is not quiet." }
              ]
            }
            """;

        var loaded = await session.LoadKnowledgePackAsync(pack, TestContext.Current.CancellationToken);
        Assert.True(loaded.IsSuccess, loaded.Error);

        var result = await session.SubmitAsync(
            "I have a limited budget and no car. I would prefer somewhere quiet and outdoors. Help me choose a place. My budget is $25.",
            TestContext.Current.CancellationToken);

        var status = await session.GetStatusAsync(TestContext.Current.CancellationToken);
        var evaluation = Assert.IsType<LatticeObjectiveEvaluation>(status.ObjectiveEvaluation);
        Assert.Equal(3, evaluation.Candidates.Length);

        var quiet = Assert.Single(evaluation.Candidates, candidate => candidate.Candidate == "Quiet Park");
        Assert.False(quiet.HasHardConstraintViolation);
        Assert.False(quiet.HasUnresolvedHardConstraint);
        Assert.Equal("Eligible", quiet.Feasibility);
        Assert.All(quiet.Conditions, condition => Assert.Equal("Satisfied", condition.Status));
        Assert.Contains(
            quiet.Conditions.SelectMany(condition => condition.Evidence),
            source => source.SourceText == "Quiet Park is quiet and outdoors.");

        var busy = Assert.Single(evaluation.Candidates, candidate => candidate.Candidate == "Busy Park");
        Assert.True(busy.HasHardConstraintViolation);
        Assert.Equal("Excluded", busy.Feasibility);
        Assert.False(busy.HasUnresolvedHardConstraint);
        Assert.Contains(busy.Conditions, condition => condition.Name == "preference" && condition.Status == "PartiallySatisfied");
        var noisy = Assert.Single(evaluation.Candidates, candidate => candidate.Candidate == "Noisy Park");
        Assert.Equal("Eligible", noisy.Feasibility);
        Assert.Contains(noisy.Conditions.SelectMany(condition => condition.Preferences),
            preference => preference.Name == "quiet" && preference.Status == "Violated");

        var tradeOff = Assert.Single(evaluation.PreferenceTradeOffs);
        Assert.Equal("quiet", tradeOff.Preference);
        Assert.Equal("Quiet Park", tradeOff.CandidateWithPositiveEvidence);
        Assert.Equal("Noisy Park", tradeOff.CandidateWithContraryEvidence);
        Assert.Equal("UniquePreferenceLeader", evaluation.Recommendation.Status);
        Assert.Equal("Quiet Park", Assert.Single(evaluation.Recommendation.ShortlistedCandidates));
        Assert.Equal("Noisy Park", Assert.Single(evaluation.Recommendation.DominatedCandidates));
        Assert.False(evaluation.Recommendation.HasIncompletePreferenceEvidence);
        Assert.Contains(tradeOff.PositiveEvidence, source =>
            source.SourceText == "Quiet Park is quiet and outdoors.");
        Assert.Contains(tradeOff.ContraryEvidence, source =>
            source.SourceText == "Noisy Park is outdoors and is not quiet.");

        var message = result.Utterances.Single().Message;
        Assert.Contains("Quiet Park: is eligible under the assessed hard constraints", message);
        Assert.Contains("Busy Park: is excluded by a hard constraint", message);
        Assert.Contains("Quiet Park has evidence for quiet, while Noisy Park has evidence against it.", message);
        Assert.Contains("Supporting evidence: Quiet Park is quiet and outdoors.", message);
        Assert.Contains("Contrary evidence: Noisy Park is outdoors and is not quiet.", message);
        Assert.Contains("Only non-dominated eligible candidate on the stated preferences: Quiet Park.", message);
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
    public async Task Laptop_comparison_keeps_unsupported_preferences_unknown_individually()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);
        var pack = await session.LoadKnowledgePackAsync(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "laptop-candidates.json")),
            TestContext.Current.CancellationToken);
        Assert.True(pack.IsSuccess, pack.Error);

        const string input = "I'm looking for a laptop that will last a long time. I don't need gaming performance, but I care about screen quality, reliability, and repairability. Help me compare these three options. My budget is $1,000.";
        var result = await session.SubmitAsync(input, TestContext.Current.CancellationToken);
        var status = await session.GetStatusAsync(TestContext.Current.CancellationToken);
        var evaluation = Assert.IsType<LatticeObjectiveEvaluation>(status.ObjectiveEvaluation);

        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        var aster = Assert.Single(evaluation.Candidates, candidate => candidate.Candidate == "Aster 14");
        var birch = Assert.Single(evaluation.Candidates, candidate => candidate.Candidate == "Birch 15");
        var cedar = Assert.Single(evaluation.Candidates, candidate => candidate.Candidate == "Cedar 14");

        var asterPreference = Assert.Single(aster.Conditions, condition => condition.Name == "preference");
        var birchPreference = Assert.Single(birch.Conditions, condition => condition.Name == "preference");
        var cedarPreference = Assert.Single(cedar.Conditions, condition => condition.Name == "preference");

        Assert.Equal("Unknown", asterPreference.Status);
        Assert.Equal("Unknown", birchPreference.Status);
        Assert.All(asterPreference.Preferences, preference => Assert.Equal("Unknown", preference.Status));
        Assert.All(birchPreference.Preferences, preference => Assert.Equal("Unknown", preference.Status));
        Assert.All(asterPreference.Preferences, preference => Assert.Empty(preference.Evidence));

        Assert.Equal("PartiallySatisfied", cedarPreference.Status);
        Assert.Collection(
            cedarPreference.Preferences,
            preference =>
            {
                Assert.Equal("screen quality", preference.Name);
                Assert.Equal("Unknown", preference.Status);
                Assert.Empty(preference.Evidence);
            },
            preference =>
            {
                Assert.Equal("reliability", preference.Name);
                Assert.Equal("Unknown", preference.Status);
                Assert.Empty(preference.Evidence);
            },
            preference =>
            {
                Assert.Equal("repairability", preference.Name);
                Assert.Equal("Satisfied", preference.Status);
                Assert.Contains(
                    preference.Evidence,
                    source => source.SourceText == "Cedar 14 has an upgrade path.");
            });

        var message = result.Utterances.Single().Message;
        Assert.Contains("some preferences are evidenced; others remain unknown", message);
        Assert.Contains("repairability: satisfied", message);
        Assert.Contains("screen quality: unknown from supplied data", message);
        Assert.DoesNotContain("reliability: not satisfied", message);
    }

    [Fact]
    public async Task Revises_one_preference_and_budget_without_losing_other_objective_conditions()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);
        var pack = await session.LoadKnowledgePackAsync(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "laptop-candidates.json")),
            TestContext.Current.CancellationToken);
        Assert.True(pack.IsSuccess, pack.Error);

        const string originalRequest = "I'm looking for a laptop that will last a long time. I don't need gaming performance, but I care about screen quality, reliability, and repairability. Help me compare these three options. My budget is $1,000.";
        await session.SubmitAsync(originalRequest, TestContext.Current.CancellationToken);

        var preferenceRevision = await session.SubmitAsync(
            "Actually, I prefer repairability.",
            TestContext.Current.CancellationToken);
        var afterPreferenceRevision = await session.GetStatusAsync(TestContext.Current.CancellationToken);
        var preferenceEvaluation = Assert.IsType<LatticeObjectiveEvaluation>(
            afterPreferenceRevision.ObjectiveEvaluation);
        var cedarAfterPreference = Assert.Single(
            preferenceEvaluation.Candidates,
            candidate => candidate.Candidate == "Cedar 14");
        var revisedPreference = Assert.Single(
            cedarAfterPreference.Conditions,
            condition => condition.Name == "preference");

        Assert.Equal(LatticeUtteranceResultKind.ObjectiveInformationReceived,
            Assert.Single(preferenceRevision.Utterances).Kind);
        Assert.Single(revisedPreference.Preferences);
        Assert.Equal("repairability", revisedPreference.Preferences[0].Name);
        Assert.Equal("Satisfied", revisedPreference.Preferences[0].Status);
        Assert.Contains(
            "hardconstraint(spending-limit, atmost, $1000)",
            preferenceRevision.Utterances.Single().Meaning);
        Assert.Contains(
            "preference(preference, desires, repairability)",
            preferenceRevision.Utterances.Single().Meaning);

        var budgetRevision = await session.SubmitAsync(
            "Actually, my budget is $800.",
            TestContext.Current.CancellationToken);
        var afterBudgetRevision = await session.GetStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal(LatticeUtteranceResultKind.ObjectiveInformationReceived,
            Assert.Single(budgetRevision.Utterances).Kind);
        Assert.Contains("hardconstraint(spending-limit, atmost, $800)",
            budgetRevision.Utterances.Single().Meaning);
        Assert.DoesNotContain("hardconstraint(spending-limit, atmost, $1000)",
            budgetRevision.Utterances.Single().Meaning);
        Assert.Equal("compare these three options", afterBudgetRevision.ActiveObjective);
        Assert.Contains(originalRequest, afterBudgetRevision.ActiveObjectiveSource);
        Assert.Contains("Actually, I prefer repairability.", afterBudgetRevision.ActiveObjectiveSource);
        Assert.Contains("Actually, my budget is $800.", afterBudgetRevision.ActiveObjectiveSource);

        var finalEvaluation = Assert.IsType<LatticeObjectiveEvaluation>(
            afterBudgetRevision.ObjectiveEvaluation);
        var cedarFinal = Assert.Single(finalEvaluation.Candidates, candidate => candidate.Candidate == "Cedar 14");
        Assert.Single(Assert.Single(cedarFinal.Conditions, condition => condition.Name == "preference").Preferences);
        Assert.Equal("Unknown",
            Assert.Single(cedarFinal.Conditions, condition => condition.Name == "spending-limit").Status);
    }

    [Fact]
    public async Task Adds_and_removes_individual_preferences_without_replacing_the_set()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);
        var pack = await session.LoadKnowledgePackAsync(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "laptop-candidates.json")),
            TestContext.Current.CancellationToken);
        Assert.True(pack.IsSuccess, pack.Error);

        const string request = "I'm looking for a laptop that will last a long time. I don't need gaming performance, but I care about screen quality, reliability, and repairability. Help me compare these three options. My budget is $1,000.";
        await session.SubmitAsync(request, TestContext.Current.CancellationToken);

        var added = await session.SubmitAsync(
            "Also, I care about battery life.",
            TestContext.Current.CancellationToken);
        var afterAdd = await session.GetStatusAsync(TestContext.Current.CancellationToken);
        var addMeaning = added.Utterances.Single().Meaning;
        Assert.Contains("preference(preference, desires, screen quality+reliability+repairability+battery life)", addMeaning);

        var cedarAfterAdd = Assert.Single(
            Assert.IsType<LatticeObjectiveEvaluation>(afterAdd.ObjectiveEvaluation).Candidates,
            candidate => candidate.Candidate == "Cedar 14");
        var preferencesAfterAdd = Assert.Single(
            cedarAfterAdd.Conditions,
            condition => condition.Name == "preference").Preferences;
        Assert.Equal(
            ["screen quality", "reliability", "repairability", "battery life"],
            preferencesAfterAdd.Select(preference => preference.Name));
        Assert.Equal("Satisfied", Assert.Single(
            preferencesAfterAdd,
            preference => preference.Name == "repairability").Status);
        Assert.Equal("Unknown", Assert.Single(
            preferencesAfterAdd,
            preference => preference.Name == "battery life").Status);

        await session.SubmitAsync(
            "Also, I care about battery life.",
            TestContext.Current.CancellationToken);
        var afterDuplicate = await session.GetStatusAsync(TestContext.Current.CancellationToken);
        var cedarAfterDuplicate = Assert.Single(
            Assert.IsType<LatticeObjectiveEvaluation>(afterDuplicate.ObjectiveEvaluation).Candidates,
            candidate => candidate.Candidate == "Cedar 14");
        Assert.Equal(4, Assert.Single(
            cedarAfterDuplicate.Conditions,
            condition => condition.Name == "preference").Preferences.Length);

        var removed = await session.SubmitAsync(
            "Remove screen quality from my preferences.",
            TestContext.Current.CancellationToken);
        var afterRemove = await session.GetStatusAsync(TestContext.Current.CancellationToken);
        Assert.Contains("preference(preference, desires, reliability+repairability+battery life)", removed.Utterances.Single().Meaning);
        Assert.DoesNotContain("screen quality", removed.Utterances.Single().Meaning);

        var cedarAfterRemove = Assert.Single(
            Assert.IsType<LatticeObjectiveEvaluation>(afterRemove.ObjectiveEvaluation).Candidates,
            candidate => candidate.Candidate == "Cedar 14");
        var remainingPreferences = Assert.Single(
            cedarAfterRemove.Conditions,
            condition => condition.Name == "preference").Preferences;
        Assert.Equal(
            ["reliability", "repairability", "battery life"],
            remainingPreferences.Select(preference => preference.Name));
        Assert.Equal("Satisfied", Assert.Single(
            remainingPreferences,
            preference => preference.Name == "repairability").Status);
        Assert.Equal("Unknown", Assert.Single(
            remainingPreferences,
            preference => preference.Name == "battery life").Status);

        await session.SubmitAsync(
            "Remove screen quality from my preferences.",
            TestContext.Current.CancellationToken);
        var afterAbsentRemoval = await session.GetStatusAsync(TestContext.Current.CancellationToken);
        var cedarAfterAbsentRemoval = Assert.Single(
            Assert.IsType<LatticeObjectiveEvaluation>(afterAbsentRemoval.ObjectiveEvaluation).Candidates,
            candidate => candidate.Candidate == "Cedar 14");
        Assert.Equal(3, Assert.Single(
            cedarAfterAbsentRemoval.Conditions,
            condition => condition.Name == "preference").Preferences.Length);
        Assert.Contains(request, afterAbsentRemoval.ActiveObjectiveSource);
        Assert.Contains("Also, I care about battery life.", afterAbsentRemoval.ActiveObjectiveSource);
        Assert.Contains("Remove screen quality from my preferences.", afterAbsentRemoval.ActiveObjectiveSource);
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
        Assert.Contains("hardconstraint(time-window, canfit, 09:00-18:00)", meaning);
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
        Assert.Contains("hardconstraint(time-window, canfit, 09:00-18:00)", meaning);
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
                { "subject": "Bus Park", "name": "origin", "value": { "type": "text", "value": "Central Station" }, "sourceText": "The supplied bus journey is from Central Station." },
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
                { "subject": "Walk Park", "name": "origin", "value": { "type": "text", "value": "Central Station" }, "sourceText": "The supplied walking route is from Central Station." },
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
        Assert.Contains(busPark.Conditions, condition => condition.Name == "origin" && condition.Status == "Satisfied");
        Assert.Contains(busPark.Conditions, condition => condition.Name == "meal-plan" && condition.Status == "NotApplicable");
        Assert.Contains(
            "Candidate assessment (not ranked):",
            followUp.Utterances[^1].Message);
        Assert.Contains("Bus Park", followUp.Utterances[^1].Message);
        Assert.Contains("Walk Park", followUp.Utterances[^1].Message);
        Assert.Contains("Stage 1: 09:00–09:25", followUp.Utterances[^1].Message);
        Assert.Contains("Stage 1: 09:00–09:15", followUp.Utterances[^1].Message);
        Assert.Equal(3, Assert.Single(busPark.Conditions, condition => condition.Name == "time-window").TemporalWitness.Length);
        Assert.Equal(3, Assert.Single(walkPark.Conditions, condition => condition.Name == "time-window").TemporalWitness.Length);

        var walkingOnly = await session.SubmitAsync(
            "Actually, I don't want to take a bus. Walking only.",
            TestContext.Current.CancellationToken);
        Assert.Equal(LatticeTurnStatus.Completed, walkingOnly.Status);

        var afterWalkingOnly = await session.GetStatusAsync(TestContext.Current.CancellationToken);
        var revisedEvaluation = Assert.IsType<LatticeObjectiveEvaluation>(afterWalkingOnly.ObjectiveEvaluation);
        busPark = Assert.Single(revisedEvaluation.Candidates, candidate => candidate.Candidate == "Bus Park");
        walkPark = Assert.Single(revisedEvaluation.Candidates, candidate => candidate.Candidate == "Walk Park");
        Assert.True(busPark.HasHardConstraintViolation);
        Assert.Equal("Excluded", busPark.Feasibility);
        Assert.False(walkPark.HasHardConstraintViolation);
        Assert.Equal("Eligible", walkPark.Feasibility);
        Assert.Contains(walkPark.Conditions, condition => condition.Name == "time-window" && condition.Status == "Satisfied");
        Assert.Contains(walkPark.Conditions, condition => condition.Name == "meal-plan" && condition.Status == "NotApplicable");
        Assert.Contains(
            "Bus Park: is excluded by a hard constraint",
            walkingOnly.Utterances[^1].Message);
        Assert.Contains(
            "Walk Park: is eligible under the assessed hard constraints",
            walkingOnly.Utterances[^1].Message);
        Assert.Contains("Stage 1: 09:00–09:15", walkingOnly.Utterances[^1].Message);
        Assert.DoesNotContain("Stage 1: 09:00–09:25", walkingOnly.Utterances[^1].Message);
        Assert.Contains("Walking only.", afterWalkingOnly.ActiveObjectiveSource);
    }

    [Fact]
    public async Task Reuses_objective_evaluation_for_laptops_without_inventing_missing_evidence()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);
        var pack = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "laptop-candidates.json"),
            TestContext.Current.CancellationToken);

        var loaded = await session.LoadKnowledgePackAsync(pack, TestContext.Current.CancellationToken);
        Assert.True(loaded.IsSuccess, loaded.Error);

        const string request = "I'm looking for a laptop that will last a long time. I don't need gaming performance, but I care about screen quality, reliability, and repairability. Help me compare these three options. My budget is $1,000.";
        var result = await session.SubmitAsync(request, TestContext.Current.CancellationToken);
        var status = await session.GetStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.ObjectiveReceived, Assert.Single(result.Utterances).Kind);
        Assert.Equal(request, status.ActiveObjectiveSource);

        var evaluation = Assert.IsType<LatticeObjectiveEvaluation>(status.ObjectiveEvaluation);
        Assert.Equal(3, evaluation.Candidates.Length);
        Assert.All(evaluation.Candidates, candidate =>
        {
            Assert.False(candidate.HasHardConstraintViolation);
            Assert.True(candidate.HasUnresolvedHardConstraint);
            Assert.Contains(candidate.Conditions, condition =>
                condition.Name == "spending-limit" && condition.Status == "Unknown");
        });

        var aster = Assert.Single(evaluation.Candidates, candidate => candidate.Candidate == "Aster 14");
        var birch = Assert.Single(evaluation.Candidates, candidate => candidate.Candidate == "Birch 15");
        var cedar = Assert.Single(evaluation.Candidates, candidate => candidate.Candidate == "Cedar 14");
        Assert.Contains(aster.Conditions, condition => condition.Name == "preference" && condition.Status == "Unknown");
        Assert.Contains(birch.Conditions, condition => condition.Name == "preference" && condition.Status == "Unknown");
        Assert.Contains(cedar.Conditions, condition => condition.Name == "preference" && condition.Status == "PartiallySatisfied");
        Assert.Contains(
            cedar.Conditions.SelectMany(condition => condition.Evidence),
            source => source.SourceText == "Cedar 14 has an upgrade path.");
        Assert.DoesNotContain(
            evaluation.Candidates.SelectMany(candidate => candidate.Conditions).SelectMany(condition => condition.Evidence),
            source => source.SourceText.Contains("reliability", StringComparison.OrdinalIgnoreCase) ||
                source.SourceText.Contains("warranty", StringComparison.OrdinalIgnoreCase));
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
