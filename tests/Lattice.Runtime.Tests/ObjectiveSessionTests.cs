using AwesomeAssertions;
using System.Collections.Immutable;
using System.Reflection;
using Lattice.Client;
using Lattice.Core.Semantics;
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
                { "subject": "Quiet Park", "name": "main-activity", "value": { "type": "text", "value": "Marked 2-hour loop" }, "sourceText": "The main activity is a marked 2-hour loop.", "summary": true },
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
        var highlight = Assert.Single(quiet.Highlights);
        Assert.Equal("main-activity", highlight.Name);
        Assert.Contains(highlight.Evidence, source =>
            source.SourceText == "The main activity is a marked 2-hour loop.");

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
        Assert.Contains("I'd recommend Quiet Park", message);
        Assert.Contains("marked 2-hour loop", message);
        Assert.DoesNotContain("Candidate assessment", message);
        Assert.Contains("Quiet Park is quiet and outdoors.", status.ObjectiveExplanation);
        Assert.Contains("Noisy Park is outdoors and is not quiet.", status.ObjectiveExplanation);
    }

    [Fact]
    public async Task Presents_preference_tradeoffs_as_a_concise_choice_and_keeps_the_evidence_for_why()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);
        const string pack = """
            {
              "formatVersion": 1,
              "id": "objective-preference-tradeoff",
              "name": "Objective preference trade-off",
              "claims": [],
              "attributes": [
                { "subject": "Quiet Grove", "name": "transport-mode", "value": { "type": "text", "value": "walking" }, "sourceText": "Quiet Grove is reachable on foot." },
                { "subject": "Quiet Grove", "name": "tags", "value": { "type": "textSet", "values": ["quiet"], "excludedValues": ["outdoors"] }, "sourceText": "Quiet Grove is quiet but not outdoors." },
                { "subject": "Open Meadow", "name": "transport-mode", "value": { "type": "text", "value": "walking" }, "sourceText": "Open Meadow is reachable on foot." },
                { "subject": "Open Meadow", "name": "tags", "value": { "type": "textSet", "values": ["outdoors"], "excludedValues": ["quiet"] }, "sourceText": "Open Meadow is outdoors but not quiet." }
              ]
            }
            """;

        var loaded = await session.LoadKnowledgePackAsync(pack, TestContext.Current.CancellationToken);
        Assert.True(loaded.IsSuccess, loaded.Error);

        var result = await session.SubmitAsync(
            "I have no car. I would prefer somewhere quiet and outdoors. Help me choose a place.",
            TestContext.Current.CancellationToken);
        var status = await session.GetStatusAsync(TestContext.Current.CancellationToken);
        var evaluation = Assert.IsType<LatticeObjectiveEvaluation>(status.ObjectiveEvaluation);

        Assert.Equal("MultipleNonDominatedCandidates", evaluation.Recommendation.Status);
        Assert.Equal(
            new[] { "Open Meadow", "Quiet Grove" },
            evaluation.Recommendation.ShortlistedCandidates);
        Assert.Equal(2, evaluation.PreferenceTradeOffs.Length);

        var message = result.Utterances.Single().Message;
        Assert.Contains("You have two good options: Open Meadow and Quiet Grove.", message);
        Assert.Contains("Quiet Grove has stronger evidence for quiet than Open Meadow.", message);
        Assert.Contains("Open Meadow has stronger evidence for outdoors than Quiet Grove.", message);
        Assert.DoesNotContain("Evidence:", message);
        Assert.Contains("Quiet Grove is quiet but not outdoors.", status.ObjectiveExplanation);
        Assert.Contains("Open Meadow is outdoors but not quiet.", status.ObjectiveExplanation);
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
        Assert.Equal(4, status.FactCount);
        Assert.Equal(4, status.RuleCount);

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
    public async Task Explains_when_every_candidate_violates_known_hard_constraints_and_asks_what_to_relax()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);
        const string pack = """
            {
              "formatVersion": 1,
              "id": "infeasible-objective-test",
              "name": "Infeasible objective test",
              "claims": [],
              "attributes": [
                { "subject": "Over Budget Park", "name": "total-cost", "value": { "type": "money", "amount": 40, "currencySymbol": "$" }, "sourceText": "Over Budget Park costs $40." },
                { "subject": "Over Budget Park", "name": "transport-mode", "value": { "type": "text", "value": "walking" }, "sourceText": "Over Budget Park is reachable by walking." },
                { "subject": "Over Budget Park", "name": "tags", "value": { "type": "textSet", "values": ["quiet", "outdoors"] }, "sourceText": "Over Budget Park is quiet and outdoors." },
                { "subject": "Car Only Park", "name": "total-cost", "value": { "type": "money", "amount": 10, "currencySymbol": "$" }, "sourceText": "Car Only Park costs $10." },
                { "subject": "Car Only Park", "name": "transport-mode", "value": { "type": "text", "value": "car" }, "sourceText": "Car Only Park requires a car." },
                { "subject": "Car Only Park", "name": "tags", "value": { "type": "textSet", "values": ["quiet", "outdoors"] }, "sourceText": "Car Only Park is quiet and outdoors." }
              ]
            }
            """;

        var loaded = await session.LoadKnowledgePackAsync(pack, TestContext.Current.CancellationToken);
        Assert.True(loaded.IsSuccess, loaded.Error);

        var result = await session.SubmitAsync(
            "I have a free Saturday, a limited budget, and no car. I would prefer somewhere quiet and outdoors. Help me put together a good day. My budget is $25.",
            TestContext.Current.CancellationToken);

        var status = await session.GetStatusAsync(TestContext.Current.CancellationToken);
        var evaluation = Assert.IsType<LatticeObjectiveEvaluation>(status.ObjectiveEvaluation);
        Assert.Equal("NoEligibleCandidates", evaluation.Recommendation.Status);
        Assert.All(evaluation.Candidates, candidate => Assert.Equal("Excluded", candidate.Feasibility));
        Assert.Empty(evaluation.EvidenceRequests);

        var message = result.Utterances.Single().Message;
        Assert.Contains("None of the supplied options currently meet all your requirements.", message);
        Assert.Contains("spending limit", message);
        Assert.Contains("transport", message);
        Assert.DoesNotContain("Candidate assessment", message);
        var carOnly = Assert.Single(evaluation.Candidates, candidate => candidate.Candidate == "Car Only Park");
        var carViolations = carOnly.Conditions
            .Where(condition => condition.Status == "Violated")
            .Select(condition => string.Join(
                ' ',
                condition.Name.Split(new[] { '-', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(word => char.ToUpperInvariant(word[0]) + word[1..])))
            .ToArray();
        Assert.NotEmpty(carViolations);
        Assert.Contains("Transport: violated by Car Only Park.", status.ObjectiveExplanation);
        Assert.Contains("Would you like to relax one of these constraints?", status.ObjectiveExplanation);
        Assert.DoesNotContain("Unresolved candidates still need evidence:", message);
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
        Assert.Equal(4, status.FactCount);
        Assert.Equal(4, status.RuleCount);
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

        Assert.Equal(
            new[] { "Aster 14", "Birch 15", "Cedar 14" },
            evaluation.EvidenceRequests.Select(request => request.Candidate));
        Assert.All(evaluation.EvidenceRequests, request =>
        {
            Assert.Equal("spending-limit", request.ConditionName);
            Assert.Equal("total-cost", request.AttributeName);
            Assert.StartsWith("What is the total cost for ", request.Prompt);
        });
        Assert.Contains("I can't recommend an option yet.", result.Utterances.Single().Message);
        Assert.Contains("What is the total cost for Aster 14?", result.Utterances.Single().Message);
        Assert.DoesNotContain("Candidate assessment", result.Utterances.Single().Message);

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
        Assert.Contains("I can't recommend an option yet.", message);
        Assert.DoesNotContain("repairability: satisfied", message);
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
            "Correction: I prefer repairability.",
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
        Assert.Contains("Correction: I prefer repairability.", afterBudgetRevision.ActiveObjectiveSource);
        Assert.Contains("Actually, my budget is $800.", afterBudgetRevision.ActiveObjectiveSource);

        var finalEvaluation = Assert.IsType<LatticeObjectiveEvaluation>(
            afterBudgetRevision.ObjectiveEvaluation);
        var cedarFinal = Assert.Single(finalEvaluation.Candidates, candidate => candidate.Candidate == "Cedar 14");
        Assert.Single(Assert.Single(cedarFinal.Conditions, condition => condition.Name == "preference").Preferences);
        Assert.Equal("Unknown",
            Assert.Single(cedarFinal.Conditions, condition => condition.Name == "spending-limit").Status);

        var secondBudgetRevision = await session.SubmitAsync(
            "Actually, my budget is $700.",
            TestContext.Current.CancellationToken);
        Assert.Equal(LatticeUtteranceResultKind.ObjectiveInformationReceived,
            Assert.Single(secondBudgetRevision.Utterances).Kind);
        Assert.Contains("hardconstraint(spending-limit, atmost, $700)",
            secondBudgetRevision.Utterances.Single().Meaning);
        Assert.DoesNotContain("hardconstraint(spending-limit, atmost, $800)",
            secondBudgetRevision.Utterances.Single().Meaning);
        Assert.Contains("preference(preference, desires, repairability)",
            secondBudgetRevision.Utterances.Single().Meaning);

        var secondPreferenceRevision = await session.SubmitAsync(
            "Actually, I prefer screen quality and reliability.",
            TestContext.Current.CancellationToken);
        Assert.Equal(LatticeUtteranceResultKind.ObjectiveInformationReceived,
            Assert.Single(secondPreferenceRevision.Utterances).Kind);
        Assert.Contains("hardconstraint(spending-limit, atmost, $700)",
            secondPreferenceRevision.Utterances.Single().Meaning);
        Assert.Contains("preference(preference, desires, screen quality+reliability)",
            secondPreferenceRevision.Utterances.Single().Meaning);
        Assert.DoesNotContain("preference(preference, desires, repairability)",
            secondPreferenceRevision.Utterances.Single().Meaning);

        var afterRepeatedRevisions = await session.GetStatusAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Actually, my budget is $700.", afterRepeatedRevisions.ActiveObjectiveSource);
        Assert.Contains("Actually, I prefer screen quality and reliability.", afterRepeatedRevisions.ActiveObjectiveSource);
        var repeatedEvaluation = Assert.IsType<LatticeObjectiveEvaluation>(afterRepeatedRevisions.ObjectiveEvaluation);
        var repeatedCedar = Assert.Single(repeatedEvaluation.Candidates, candidate => candidate.Candidate == "Cedar 14");
        Assert.Equal(
            ["screen quality", "reliability"],
            Assert.Single(repeatedCedar.Conditions, condition => condition.Name == "preference")
                .Preferences.Select(preference => preference.Name));
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
        Assert.Equal(4, status.FactCount);
        Assert.Equal(4, status.RuleCount);
    }

    [Fact]
    public async Task Clarifies_when_a_typed_answer_matches_multiple_pending_conditions()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var spendingLimit = new ObjectiveCondition(
            "spending-limit",
            ObjectiveConditionRole.HardConstraint,
            ObjectiveConditionOperator.AtMost,
            null,
            "limited budget")
        {
            TargetAttribute = "total-cost",
            ExpectedValueKind = ObjectiveConditionValueKind.Money,
            MissingValuePrompt = "What is your spending limit?"
        };
        var activityLimit = new ObjectiveCondition(
            "activity-limit",
            ObjectiveConditionRole.HardConstraint,
            ObjectiveConditionOperator.AtMost,
            null,
            "limited activity budget")
        {
            TargetAttribute = "activity-cost",
            ExpectedValueKind = ObjectiveConditionValueKind.Money,
            MissingValuePrompt = "What is your activity budget?"
        };
        var objective = new ObjectiveFormula("plan an outing")
        {
            Conditions = ImmutableArray.Create(spendingLimit, activityLimit)
        };

        var runtimeType = session.GetType();
        const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        runtimeType.GetField("activeObjective", privateInstance)!.SetValue(session, objective);
        runtimeType.GetField("pendingObjectiveCondition", privateInstance)!.SetValue(session, spendingLimit);

        var result = await session.SubmitAsync("$25 total.", TestContext.Current.CancellationToken);
        var utterance = Assert.Single(result.Utterances);

        Assert.Equal(LatticeUtteranceResultKind.ClarificationRequired, utterance.Kind);
        Assert.Contains("spending-limit", utterance.Message);
        Assert.Contains("activity-limit", utterance.Message);
        Assert.Contains("What is your spending limit?", utterance.Message);
        Assert.Contains("What is your activity budget?", utterance.Message);
        Assert.Contains("No objective conditions were changed.", utterance.Message);

        var retainedObjective = Assert.IsType<ObjectiveFormula>(
            runtimeType.GetField("activeObjective", privateInstance)!.GetValue(session));
        Assert.All(retainedObjective.Conditions, condition => Assert.Null(condition.Value));
    }

    [Fact]
    public async Task Applies_a_typed_answer_to_the_only_compatible_pending_condition()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var origin = new ObjectiveCondition(
            "origin",
            ObjectiveConditionRole.HardConstraint,
            ObjectiveConditionOperator.Equals,
            null,
            "starting point")
        {
            TargetAttribute = "origin",
            ExpectedValueKind = ObjectiveConditionValueKind.Text,
            MissingValuePrompt = "Where are you starting?"
        };
        var spendingLimit = new ObjectiveCondition(
            "spending-limit",
            ObjectiveConditionRole.HardConstraint,
            ObjectiveConditionOperator.AtMost,
            null,
            "limited budget")
        {
            TargetAttribute = "total-cost",
            ExpectedValueKind = ObjectiveConditionValueKind.Money,
            MissingValuePrompt = "What is your spending limit?"
        };
        var objective = new ObjectiveFormula("plan an outing")
        {
            Conditions = ImmutableArray.Create(origin, spendingLimit)
        };

        var runtimeType = session.GetType();
        const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        runtimeType.GetField("activeObjective", privateInstance)!.SetValue(session, objective);
        runtimeType.GetField("pendingObjectiveCondition", privateInstance)!.SetValue(session, origin);

        var result = await session.SubmitAsync("$25 total.", TestContext.Current.CancellationToken);
        var utterance = Assert.Single(result.Utterances);

        Assert.Equal(LatticeUtteranceResultKind.ObjectiveInformationReceived, utterance.Kind);
        Assert.Contains("for spending-limit", utterance.Message);

        var updatedObjective = Assert.IsType<ObjectiveFormula>(
            runtimeType.GetField("activeObjective", privateInstance)!.GetValue(session));
        Assert.Null(updatedObjective.Conditions.Single(condition => condition.Name == "origin").Value);
        Assert.Equal(
            new ObjectiveMoneyValue(25m, "$"),
            updatedObjective.Conditions.Single(condition => condition.Name == "spending-limit").Value);
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
        Assert.Equal(4, status.FactCount);
    }

    [Fact]
    public async Task Accepts_a_natural_language_spending_limit_answer_in_the_active_objective()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);
        const string originalRequest = "I have a free Saturday, a limited budget, and no car. I would prefer somewhere quiet and outdoors. Help me put together a good day.";

        var initial = await session.SubmitAsync(originalRequest, TestContext.Current.CancellationToken);
        Assert.Equal(
            LatticeUtteranceResultKind.ClarificationRequired,
            Assert.Single(initial.Utterances).Kind);

        var answer = await session.SubmitAsync(
            "I can spend up to $25.",
            TestContext.Current.CancellationToken);
        var status = await session.GetStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal(LatticeTurnStatus.Completed, answer.Status);
        var utterance = Assert.Single(answer.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.ObjectiveInformationReceived, utterance.Kind);
        Assert.Contains("hardconstraint(spending-limit, atmost, $25)", utterance.Meaning);
        Assert.Contains("I can spend up to $25.", status.ActiveObjectiveSource);
        Assert.Null(status.PendingInformationName);
        Assert.Null(status.PendingInformationPrompt);
        Assert.Equal(4, status.FactCount);
        Assert.Equal(4, status.RuleCount);
    }

    [Fact]
    public async Task Accepts_packed_lunch_and_additional_origin_paraphrases_in_one_follow_up()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);
        const string originalRequest = "I have a free Saturday, a limited budget, and no car. I would prefer somewhere quiet and outdoors. Help me put together a good day.";
        const string followUp = "$25 total. Lunch is covered. I'm coming from Central Station, and I can leave at 9:00 and be back by 18:00.";

        await session.SubmitAsync(originalRequest, TestContext.Current.CancellationToken);
        var result = await session.SubmitAsync(followUp, TestContext.Current.CancellationToken);
        var status = await session.GetStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(3, result.Utterances.Length);
        Assert.Contains(result.Utterances.Select(utterance => utterance.Meaning),
            meaning => meaning.Contains("hardconstraint(meal-plan, equals, bring-lunch)", StringComparison.Ordinal));
        Assert.Contains(result.Utterances.Select(utterance => utterance.Meaning),
            meaning => meaning.Contains("hardconstraint(origin, equals, Central Station)", StringComparison.Ordinal));
        Assert.Contains(result.Utterances.Select(utterance => utterance.Meaning),
            meaning => meaning.Contains("hardconstraint(time-window, canfit, 09:00-18:00)", StringComparison.Ordinal));
        Assert.Null(status.PendingInformationName);
        Assert.Null(status.PendingInformationPrompt);
        Assert.Equal(4, status.FactCount);
        Assert.Equal(4, status.RuleCount);
    }

    [Fact]
    public async Task Corrects_the_origin_without_replacing_other_day_out_conditions()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);
        const string originalRequest = "I have a free Saturday, a limited budget, and no car. I would prefer somewhere quiet and outdoors. Help me put together a good day.";
        const string followUp = "$25 total. Lunch is covered. I'm coming from Central Station, and I can leave at 9:00 and be back by 18:00.";

        await session.SubmitAsync(originalRequest, TestContext.Current.CancellationToken);
        await session.SubmitAsync(followUp, TestContext.Current.CancellationToken);
        var correction = await session.SubmitAsync(
            "Actually, I'm coming from North Station.",
            TestContext.Current.CancellationToken);
        var status = await session.GetStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal(LatticeTurnStatus.Completed, correction.Status);
        var utterance = Assert.Single(correction.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.ObjectiveInformationReceived, utterance.Kind);
        Assert.Contains("hardconstraint(origin, equals, North Station)", utterance.Meaning);
        Assert.DoesNotContain("hardconstraint(origin, equals, Central Station)", utterance.Meaning);
        Assert.Contains("hardconstraint(meal-plan, equals, bring-lunch)", utterance.Meaning);
        Assert.Contains("hardconstraint(time-window, canfit, 09:00-18:00)", utterance.Meaning);
        Assert.Contains("I'm coming from North Station.", status.ActiveObjectiveSource);
        Assert.Null(status.PendingInformationName);
        Assert.Null(status.PendingInformationPrompt);
        Assert.Equal(4, status.FactCount);
        Assert.Equal(4, status.RuleCount);
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
        Assert.Equal(4, status.FactCount);
        Assert.Equal(4, status.RuleCount);
    }

    [Fact]
    public async Task Accepts_an_alternative_starting_point_phrase_in_a_day_out_follow_up()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);
        const string originalRequest = "I have a free Saturday, a limited budget, and no car. I would prefer somewhere quiet and outdoors. Help me put together a good day.";

        await session.SubmitAsync(originalRequest, TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(
            "$25 total. I'll bring lunch. My starting point is Central Station. I can leave at 9:00 and be back by 18:00.",
            TestContext.Current.CancellationToken);
        var status = await session.GetStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Contains(
            result.Utterances.Select(utterance => utterance.Meaning),
            meaning => meaning.Contains("hardconstraint(origin, equals, Central Station)", StringComparison.Ordinal));
        Assert.Contains("My starting point is Central Station.", status.ActiveObjectiveSource);
        Assert.Null(status.PendingInformationName);
        Assert.Equal(4, status.FactCount);
        Assert.Equal(4, status.RuleCount);
    }

    [Fact]
    public async Task Accepts_reordered_departure_and_return_times_in_a_day_out_follow_up()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);
        const string originalRequest = "I have a free Saturday, a limited budget, and no car. I would prefer somewhere quiet and outdoors. Help me put together a good day.";
        const string followUp = "$25 total. I can be back by 18:00 and leave at 9:00. I'm starting at Central Station. I'll bring lunch.";

        await session.SubmitAsync(originalRequest, TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(followUp, TestContext.Current.CancellationToken);
        var status = await session.GetStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(4, result.Utterances.Length);
        Assert.Contains(
            result.Utterances.Select(utterance => utterance.Meaning),
            meaning => meaning.Contains("hardconstraint(time-window, canfit, 09:00-18:00)", StringComparison.Ordinal));
        Assert.Contains("I can be back by 18:00 and leave at 9:00.", status.ActiveObjectiveSource);
        Assert.Null(status.PendingInformationName);
        Assert.Null(status.PendingInformationPrompt);
        Assert.Equal(4, status.FactCount);
        Assert.Equal(4, status.RuleCount);
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
        Assert.Equal(4, status.FactCount);
        Assert.Equal(4, status.RuleCount);
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
                { "subject": "Bus Park", "name": "return-transport-cost", "value": { "type": "money", "amount": 6, "currencySymbol": "$" }, "sourceText": "The return bus fare is $6.", "summary": true },
                { "subject": "Bus Park", "name": "entry-cost", "value": { "type": "money", "amount": 0, "currencySymbol": "$" }, "sourceText": "Entry is free.", "summary": true },
                { "subject": "Bus Park", "name": "main-activity", "value": { "type": "text", "value": "Marked 2-hour loop" }, "sourceText": "The main activity is a marked 2-hour loop.", "summary": true },
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
                { "subject": "Walk Park", "name": "return-transport-cost", "value": { "type": "money", "amount": 0, "currencySymbol": "$" }, "sourceText": "Walking has no transport fare.", "summary": true },
                { "subject": "Walk Park", "name": "entry-cost", "value": { "type": "money", "amount": 0, "currencySymbol": "$" }, "sourceText": "Entry is free.", "summary": true },
                { "subject": "Walk Park", "name": "main-activity", "value": { "type": "text", "value": "Paved paths" }, "sourceText": "The main activity is walking paved paths.", "summary": true },
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
        var response = followUp.Utterances[^1].Message;
        Assert.Contains("You have two good options: Bus Park and Walk Park.", response);
        Assert.Contains("Bus 10:30–10:55", response);
        Assert.Contains("Walk 09:00–09:15", response);
        Assert.Contains("$6 return transport", response);
        Assert.Contains("marked 2-hour loop", response);
        Assert.DoesNotContain("Candidate assessment", response);
        Assert.DoesNotContain("Evidence:", response);
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
        Assert.Contains("Walk Park is the only option currently confirmed to meet your requirements.", walkingOnly.Utterances[^1].Message);
        Assert.Contains("Walk 09:00–09:15", walkingOnly.Utterances[^1].Message);
        Assert.DoesNotContain("Bus 10:30–10:55", walkingOnly.Utterances[^1].Message);
        Assert.DoesNotContain("marked 2-hour loop", walkingOnly.Utterances[^1].Message);
        Assert.Contains("paved paths", walkingOnly.Utterances[^1].Message);
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
        Assert.All(
            evaluation.Candidates.SelectMany(candidate => candidate.Conditions)
                .SelectMany(condition => condition.Preferences)
                .Where(preference => preference.Evidence.IsDefaultOrEmpty),
            preference => Assert.Equal("Unknown", preference.Status));
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
        Assert.Equal("NoEligibleCandidates", evaluation.Recommendation.Status);
        Assert.Empty(evaluation.Recommendation.ShortlistedCandidates);
        Assert.True(evaluation.Recommendation.HasIncompletePreferenceEvidence);
        var cedarHighlight = Assert.Single(cedar.Highlights);
        Assert.Equal("tags", cedarHighlight.Name);
        Assert.Contains(cedarHighlight.Evidence, source =>
            source.SourceText == "Cedar 14 has an upgrade path.");

        var message = result.Utterances[^1].Message;
        Assert.Contains("I can't recommend an option yet.", message);
        Assert.Contains("Aster 14, Birch 15, and Cedar 14", message);
        Assert.DoesNotContain("Candidate assessment", message);
        Assert.DoesNotContain("Shortlisted options", message);
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
        Assert.Equal(5, status.FactCount);
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
        Assert.Equal(4, status.FactCount);
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
        Assert.Equal(5, status.FactCount);
    }
    [Fact]
    public async Task Completes_day_out_benchmark_with_real_candidate_pack_and_walking_only_revision()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);
        var pack = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "day-out-candidates.json"),
            TestContext.Current.CancellationToken);

        var loaded = await session.LoadKnowledgePackAsync(pack, TestContext.Current.CancellationToken);
        Assert.True(loaded.IsSuccess, loaded.Error);

        var request = await session.SubmitAsync(
            "I have a free Saturday, a limited budget, and no car. I would prefer somewhere quiet and outdoors. Help me put together a good day.",
            TestContext.Current.CancellationToken);
        Assert.Equal(LatticeTurnStatus.Completed, request.Status);
        Assert.Equal(
            LatticeUtteranceResultKind.ClarificationRequired,
            Assert.Single(request.Utterances).Kind);

        var followUp = await session.SubmitAsync(
            "$25 total. I'll bring lunch. I'm starting at Central Station, and I can leave at 9:00 and be back by 18:00.",
            TestContext.Current.CancellationToken);
        Assert.Equal(LatticeTurnStatus.Completed, followUp.Status);
        Assert.Contains("Hilltop Farm Grounds might also be an option, but its total cost is still unknown.", followUp.Utterances[^1].Message);
        Assert.DoesNotContain("Candidate assessment", followUp.Utterances[^1].Message);

        var priceAnswer = await session.SubmitAsync(
            "Hilltop Farm Grounds costs $18.",
            TestContext.Current.CancellationToken);
        Assert.Equal(LatticeTurnStatus.Completed, priceAnswer.Status);
        Assert.Contains("Recorded Hilltop Farm Grounds's total cost as $18.", priceAnswer.Utterances[^1].Message);

        var status = await session.GetStatusAsync(TestContext.Current.CancellationToken);
        var evaluation = Assert.IsType<LatticeObjectiveEvaluation>(status.ObjectiveEvaluation);
        Assert.Equal("MultipleNonDominatedCandidates", evaluation.Recommendation.Status);
        Assert.Equal(
            new[] { "Fernbank Nature Reserve", "Hilltop Farm Grounds", "Quarry Lake Trail" },
            evaluation.Recommendation.ShortlistedCandidates);
        Assert.Equal("Riverside Gardens", Assert.Single(evaluation.Recommendation.DominatedCandidates));
        Assert.False(evaluation.Recommendation.HasIncompletePreferenceEvidence);
        Assert.Equal(3, evaluation.PreferenceTradeOffs.Length);
        Assert.Empty(evaluation.EvidenceRequests);
        var hilltop = Assert.Single(evaluation.Candidates, candidate => candidate.Candidate == "Hilltop Farm Grounds");
        var hilltopBudget = Assert.Single(hilltop.Conditions, condition => condition.Name == "spending-limit");
        Assert.Equal("Satisfied", hilltopBudget.Status);
        Assert.Equal("UserAssertion", Assert.Single(hilltopBudget.Evidence).Kind);

        var message = priceAnswer.Utterances[^1].Message;
        Assert.Contains("You have three good options: Fernbank Nature Reserve, Hilltop Farm Grounds, and Quarry Lake Trail.", message);
        message.Should().Contain(
            "All options meet your requirements and match your stated preferences for quiet and outdoors.");
        Assert.Contains("If you'd prefer the lower-cost, shorter option, I'd lean toward Fernbank Nature Reserve.", message);
        Assert.Contains("$6 return transport", message);
        Assert.Contains("$12 return transport", message);
        Assert.Contains("$8 return transport", message);
        Assert.Contains("marked 2-hour loop", message);
        Assert.Contains("marked 3-hour trail", message);
        Assert.Contains("grounds walk", message);
        Assert.Contains("Bus 10:30–10:55", message);
        Assert.Contains("Bus 11:15–12:00", message);
        Assert.Contains("Bus 13:00–13:35", message);
        Assert.DoesNotContain("Riverside Gardens", message);
        Assert.DoesNotContain("Candidate assessment", message);
        Assert.DoesNotContain("Evidence:", message);

        var walkingOnly = await session.SubmitAsync(
            "Actually, I don't want to take a bus. Walking only.",
            TestContext.Current.CancellationToken);
        Assert.Equal(LatticeTurnStatus.Completed, walkingOnly.Status);

        var revisedStatus = await session.GetStatusAsync(TestContext.Current.CancellationToken);
        var revisedEvaluation = Assert.IsType<LatticeObjectiveEvaluation>(revisedStatus.ObjectiveEvaluation);
        Assert.Equal("OnlyEligibleCandidate", revisedEvaluation.Recommendation.Status);
        Assert.Equal("Riverside Gardens", Assert.Single(revisedEvaluation.Recommendation.ShortlistedCandidates));

        var revisedMessage = walkingOnly.Utterances[^1].Message;
        Assert.Contains("Riverside Gardens is the only option currently confirmed to meet your requirements.", revisedMessage);
        Assert.Contains("paved riverside paths", revisedMessage);
        Assert.Contains("Walk 09:00–09:15", revisedMessage);
        Assert.Contains("Riverside Gardens doesn't match your preference for quiet.", revisedMessage);
        Assert.DoesNotContain("marked 2-hour loop", revisedMessage);
        Assert.Contains("Walking only.", revisedStatus.ActiveObjectiveSource ?? string.Empty);
        Assert.Contains("not live checks", revisedMessage);
        revisedMessage.Should().Contain("No booking has been made.");
    }

    [Fact]
    public async Task Clarifies_ambiguous_demonstrative_candidate_reference_for_text_evidence()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);
        const string pack = """
            {
              "formatVersion": 1,
              "id": "ambiguous-park-candidates",
              "name": "Ambiguous park candidates",
              "claims": [],
              "attributes": [
                {
                  "subject": "Quiet Park",
                  "name": "tags",
                  "value": { "type": "textSet", "values": ["quiet", "outdoors"] },
                  "sourceText": "Quiet Park is quiet and outdoors."
                },
                {
                  "subject": "Riverside Park",
                  "name": "tags",
                  "value": { "type": "textSet", "values": ["quiet", "outdoors"] },
                  "sourceText": "Riverside Park is quiet and outdoors."
                }
              ]
            }
            """;
        var loaded = await session.LoadKnowledgePackAsync(pack, TestContext.Current.CancellationToken);
        Assert.True(loaded.IsSuccess, loaded.Error);

        await session.SubmitAsync(
            "I have no car. I would prefer somewhere quiet and outdoors. Help me find a place.",
            TestContext.Current.CancellationToken);

        var ambiguous = await session.SubmitAsync(
            "That one's transport mode is walking.",
            TestContext.Current.CancellationToken);
        var clarification = Assert.Single(ambiguous.Utterances);

        Assert.Equal(LatticeUtteranceResultKind.ClarificationRequired, clarification.Kind);
        Assert.Contains("Which candidate do you mean?", clarification.Message);
        Assert.Contains("Quiet Park", clarification.Message);
        Assert.Contains("Riverside Park", clarification.Message);
        Assert.Contains("No candidate information was changed", clarification.Message);

        var unchanged = await session.GetStatusAsync(TestContext.Current.CancellationToken);
        var evaluation = Assert.IsType<LatticeObjectiveEvaluation>(unchanged.ObjectiveEvaluation);
        Assert.Equal(2, evaluation.EvidenceRequests.Length);
        Assert.All(evaluation.Candidates, candidate =>
            Assert.Contains(candidate.Conditions, condition =>
                condition.Name == "transport" &&
                condition.Status == "Unknown" &&
                condition.Evidence.IsDefaultOrEmpty));

        var explicitAnswer = await session.SubmitAsync(
            "Quiet Park's transport mode is walking.",
            TestContext.Current.CancellationToken);
        Assert.Equal(
            LatticeUtteranceResultKind.ObjectiveInformationReceived,
            Assert.Single(explicitAnswer.Utterances).Kind);
        Assert.Contains("Recorded Quiet Park's transport-mode as walking.", explicitAnswer.Utterances.Single().Message);
    }

    [Theory]
    [InlineData("It costs $900.")]
    [InlineData("That one costs $900.")]
    [InlineData("This one costs $900.")]
    public async Task Clarifies_ambiguous_candidate_reference_without_storing_evidence(string answer)
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);
        var pack = await session.LoadKnowledgePackAsync(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "laptop-candidates.json")),
            TestContext.Current.CancellationToken);
        Assert.True(pack.IsSuccess, pack.Error);

        const string request = "I'm looking for a laptop that will last a long time. I don't need gaming performance, but I care about screen quality, reliability, and repairability. Help me compare these three options. My budget is $1,000.";
        await session.SubmitAsync(request, TestContext.Current.CancellationToken);

        var ambiguous = await session.SubmitAsync(answer, TestContext.Current.CancellationToken);

        var clarification = Assert.Single(ambiguous.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.ClarificationRequired, clarification.Kind);
        Assert.Contains("Which candidate do you mean?", clarification.Message);
        Assert.Contains("Aster 14", clarification.Message);
        Assert.Contains("Birch 15", clarification.Message);
        Assert.Contains("Cedar 14", clarification.Message);
        Assert.Contains("No candidate information was changed", clarification.Message);

        var unchanged = await session.GetStatusAsync(TestContext.Current.CancellationToken);
        var unchangedEvaluation = Assert.IsType<LatticeObjectiveEvaluation>(unchanged.ObjectiveEvaluation);
        Assert.Equal(3, unchangedEvaluation.EvidenceRequests.Length);
        Assert.All(unchangedEvaluation.Candidates, candidate =>
            Assert.DoesNotContain(
                candidate.Conditions.SelectMany(condition => condition.Evidence),
                evidence => evidence.SourceText == answer));

        var explicitAnswer = await session.SubmitAsync(
            "Aster 14 costs $900.",
            TestContext.Current.CancellationToken);
        Assert.Equal(
            LatticeUtteranceResultKind.ObjectiveInformationReceived,
            Assert.Single(explicitAnswer.Utterances).Kind);
        Assert.Contains("Recorded Aster 14's total cost as $900.", explicitAnswer.Utterances.Single().Message);
    }

    [Fact]
    public async Task Resolves_ambiguous_candidate_pronoun_when_only_one_candidate_remains()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);
        var pack = await session.LoadKnowledgePackAsync(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "laptop-candidates.json")),
            TestContext.Current.CancellationToken);
        Assert.True(pack.IsSuccess, pack.Error);

        const string request = "I'm looking for a laptop that will last a long time. I don't need gaming performance, but I care about screen quality, reliability, and repairability. Help me compare these three options. My budget is $1,000.";
        await session.SubmitAsync(request, TestContext.Current.CancellationToken);
        await session.SubmitAsync("Aster 14 costs $900.", TestContext.Current.CancellationToken);
        await session.SubmitAsync("Birch 15 costs $950.", TestContext.Current.CancellationToken);

        var resolved = await session.SubmitAsync(
            "It costs $850.",
            TestContext.Current.CancellationToken);

        var result = Assert.Single(resolved.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.ObjectiveInformationReceived, result.Kind);
        Assert.Contains("Recorded Cedar 14's total cost as $850.", result.Message);

        var status = await session.GetStatusAsync(TestContext.Current.CancellationToken);
        var evaluation = Assert.IsType<LatticeObjectiveEvaluation>(status.ObjectiveEvaluation);
        Assert.Empty(evaluation.EvidenceRequests);
        var cedar = Assert.Single(evaluation.Candidates, candidate => candidate.Candidate == "Cedar 14");
        var cedarBudget = Assert.Single(cedar.Conditions, condition => condition.Name == "spending-limit");
        Assert.Equal("Satisfied", cedarBudget.Status);
        Assert.Contains(cedarBudget.Evidence, evidence => evidence.SourceText == "It costs $850.");
        Assert.DoesNotContain(
            evaluation.Candidates
                .Where(candidate => candidate.Candidate != "Cedar 14")
                .SelectMany(candidate => candidate.Conditions)
                .SelectMany(condition => condition.Evidence),
            evidence => evidence.SourceText == "It costs $850.");
    }

    [Fact]
    public async Task Accepts_candidate_cost_answers_only_for_requested_candidates_and_preserves_conflicts()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);
        var pack = await session.LoadKnowledgePackAsync(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "laptop-candidates.json")),
            TestContext.Current.CancellationToken);
        Assert.True(pack.IsSuccess, pack.Error);

        const string request = "I'm looking for a laptop that will last a long time. I don't need gaming performance, but I care about screen quality, reliability, and repairability. Help me compare these three options. My budget is $1,000.";
        var initialTurn = await session.SubmitAsync(request, TestContext.Current.CancellationToken);
        var initialMessage = initialTurn.Utterances.Single().Message;
        Assert.Contains("What is the total cost for Aster 14?", initialMessage);
        Assert.DoesNotContain("What is the total cost for Birch 15?", initialMessage);
        Assert.DoesNotContain("What is the total cost for Cedar 14?", initialMessage);
        Assert.Contains("I can't recommend an option yet.", initialMessage);
        Assert.DoesNotContain("Decision summary:", initialMessage);
        Assert.DoesNotContain("What is the total cost for Birch 15?", initialMessage);

        var initial = await session.GetStatusAsync(TestContext.Current.CancellationToken);
        var initialEvaluation = Assert.IsType<LatticeObjectiveEvaluation>(initial.ObjectiveEvaluation);
        Assert.Equal(3, initialEvaluation.EvidenceRequests.Length);

        var unrelated = await session.SubmitAsync(
            "Nimbus 13 costs $900.",
            TestContext.Current.CancellationToken);
        Assert.Equal(LatticeUtteranceResultKind.ClarificationRequired, Assert.Single(unrelated.Utterances).Kind);
        Assert.Contains("No candidate information was changed", unrelated.Utterances.Single().Message);
        Assert.Equal(3, Assert.IsType<LatticeObjectiveEvaluation>(
            (await session.GetStatusAsync(TestContext.Current.CancellationToken)).ObjectiveEvaluation)
            .EvidenceRequests.Length);

        var accepted = await session.SubmitAsync(
            "Aster 14 costs $900.",
            TestContext.Current.CancellationToken);
        Assert.Equal(LatticeUtteranceResultKind.ObjectiveInformationReceived, Assert.Single(accepted.Utterances).Kind);
        Assert.Contains("Recorded Aster 14's total cost as $900.", accepted.Utterances.Single().Message);
        Assert.Contains("Aster 14 is the only option currently confirmed to meet your requirements.", accepted.Utterances.Single().Message);
        Assert.Contains("The comparison is provisional because some preference details are still unknown.", accepted.Utterances.Single().Message);
        Assert.Contains("Birch 15 might also be an option, but its total cost is still unknown.", accepted.Utterances.Single().Message);
        Assert.Contains("Cedar 14 might also be an option, but its total cost is still unknown.", accepted.Utterances.Single().Message);
        Assert.DoesNotContain("What is the total cost for Birch 15?", accepted.Utterances.Single().Message);
        Assert.DoesNotContain("Still unresolved:", accepted.Utterances.Single().Message);

        var afterAccepted = await session.GetStatusAsync(TestContext.Current.CancellationToken);
        var acceptedEvaluation = Assert.IsType<LatticeObjectiveEvaluation>(afterAccepted.ObjectiveEvaluation);
        Assert.Equal(new[] { "Birch 15", "Cedar 14" },
            acceptedEvaluation.EvidenceRequests.Select(evidenceRequest => evidenceRequest.Candidate));
        var aster = Assert.Single(acceptedEvaluation.Candidates, candidate => candidate.Candidate == "Aster 14");
        var asterBudget = Assert.Single(aster.Conditions, condition => condition.Name == "spending-limit");
        Assert.Equal("Satisfied", asterBudget.Status);
        var asterEvidence = Assert.Single(asterBudget.Evidence);
        Assert.Equal("UserAssertion", asterEvidence.Kind);
        Assert.Equal("Aster 14 costs $900.", asterEvidence.SourceText);

        var repeated = await session.SubmitAsync("Aster 14 costs $900.", TestContext.Current.CancellationToken);
        Assert.Equal(LatticeUtteranceResultKind.ObjectiveInformationReceived, Assert.Single(repeated.Utterances).Kind);
        var afterRepeated = await session.GetStatusAsync(TestContext.Current.CancellationToken);
        var repeatedAster = Assert.Single(
            Assert.IsType<LatticeObjectiveEvaluation>(afterRepeated.ObjectiveEvaluation).Candidates,
            candidate => candidate.Candidate == "Aster 14");
        var repeatedBudget = Assert.Single(repeatedAster.Conditions, condition => condition.Name == "spending-limit");
        Assert.Single(repeatedBudget.Evidence);

        var conflicting = await session.SubmitAsync("Aster 14 costs $950.", TestContext.Current.CancellationToken);
        Assert.Equal(LatticeUtteranceResultKind.ObjectiveInformationReceived, Assert.Single(conflicting.Utterances).Kind);
        var afterConflict = await session.GetStatusAsync(TestContext.Current.CancellationToken);
        var conflictEvaluation = Assert.IsType<LatticeObjectiveEvaluation>(afterConflict.ObjectiveEvaluation);
        var conflictedAster = Assert.Single(conflictEvaluation.Candidates, candidate => candidate.Candidate == "Aster 14");
        var conflictedBudget = Assert.Single(conflictedAster.Conditions, condition => condition.Name == "spending-limit");
        Assert.Equal("Conflicting", conflictedBudget.Status);
        Assert.Equal(
            new[] { "Aster 14 costs $900.", "Aster 14 costs $950." },
            conflictedBudget.Evidence.Select(source => source.SourceText));
        Assert.DoesNotContain(conflictEvaluation.EvidenceRequests,
            evidenceRequest => evidenceRequest.Candidate == "Aster 14");
    }

    [Fact]
    public async Task Accepts_requested_text_candidate_attributes_with_candidate_and_attribute_validation()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);
        const string pack = """
            {
              "formatVersion": 1,
              "id": "candidate-text-evidence",
              "name": "Candidate text evidence",
              "claims": [],
              "attributes": [{
                "subject": "Quiet Park",
                "name": "tags",
                "value": { "type": "textSet", "values": ["quiet", "outdoors"] },
                "sourceText": "Quiet Park is quiet and outdoors."
              }]
            }
            """;
        var loaded = await session.LoadKnowledgePackAsync(pack, TestContext.Current.CancellationToken);
        Assert.True(loaded.IsSuccess, loaded.Error);

        var request = await session.SubmitAsync(
            "I have no car. I would prefer somewhere quiet and outdoors. Help me find a place.",
            TestContext.Current.CancellationToken);
        Assert.Equal(LatticeTurnStatus.Completed, request.Status);

        var initial = await session.GetStatusAsync(TestContext.Current.CancellationToken);
        var initialEvaluation = Assert.IsType<LatticeObjectiveEvaluation>(initial.ObjectiveEvaluation);
        var evidenceRequest = Assert.Single(initialEvaluation.EvidenceRequests);
        Assert.Equal("Quiet Park", evidenceRequest.Candidate);
        Assert.Equal("transport-mode", evidenceRequest.AttributeName);

        var unrelated = await session.SubmitAsync(
            "Other Park's transport mode is walking.",
            TestContext.Current.CancellationToken);
        Assert.Equal(LatticeUtteranceResultKind.ClarificationRequired, Assert.Single(unrelated.Utterances).Kind);
        Assert.Contains("No candidate information was changed", unrelated.Utterances.Single().Message);
        Assert.Single(Assert.IsType<LatticeObjectiveEvaluation>(
            (await session.GetStatusAsync(TestContext.Current.CancellationToken)).ObjectiveEvaluation)
            .EvidenceRequests);

        var answer = await session.SubmitAsync(
            "Quiet Park's transport mode is walking.",
            TestContext.Current.CancellationToken);
        Assert.Equal(LatticeUtteranceResultKind.ObjectiveInformationReceived, Assert.Single(answer.Utterances).Kind);
        Assert.Contains("Recorded Quiet Park's transport-mode as walking.", answer.Utterances.Single().Message);

        var finalStatus = await session.GetStatusAsync(TestContext.Current.CancellationToken);
        var finalEvaluation = Assert.IsType<LatticeObjectiveEvaluation>(finalStatus.ObjectiveEvaluation);
        Assert.Empty(finalEvaluation.EvidenceRequests);
        var candidate = Assert.Single(finalEvaluation.Candidates, item => item.Candidate == "Quiet Park");
        Assert.Equal("Eligible", candidate.Feasibility);
        var transport = Assert.Single(candidate.Conditions, condition => condition.Name == "transport");
        Assert.Equal("Satisfied", transport.Status);
        var evidence = Assert.Single(transport.Evidence);
        Assert.Equal("UserAssertion", evidence.Kind);
        Assert.Equal("Quiet Park's transport mode is walking.", evidence.SourceText);
        Assert.Equal(4, finalStatus.FactCount);
    }


    [Fact]
    public async Task Accepts_reviewed_external_candidate_price_with_source_and_retrieval_time()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);
        var pack = await session.LoadKnowledgePackAsync(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "laptop-candidates.json")),
            TestContext.Current.CancellationToken);
        Assert.True(pack.IsSuccess, pack.Error);

        await session.SubmitAsync(
            "I'm looking for a laptop that will last a long time. I don't need gaming performance, but I care about screen quality, reliability, and repairability. Help me compare these three options. My budget is $1,000.",
            TestContext.Current.CancellationToken);

        var evidence = new LatticeCandidateMoneyEvidence(
            "Aster 14",
            900m,
            "$",
            "Aster 14 is listed at $900.",
            "https://example.org/aster-14",
            new DateTimeOffset(2026, 11, 12, 7, 0, 0, TimeSpan.Zero))
        {
            FreshUntil = DateTimeOffset.UtcNow.AddHours(1)
        };
        var accepted = await session.AddReviewedCandidateMoneyEvidenceAsync(
            evidence,
            TestContext.Current.CancellationToken);

        Assert.True(accepted.IsSuccess, accepted.Error);
        Assert.Equal("Aster 14", accepted.Candidate);
        Assert.Equal("total-cost", accepted.AttributeName);

        var status = await session.GetStatusAsync(TestContext.Current.CancellationToken);
        var evaluation = Assert.IsType<LatticeObjectiveEvaluation>(status.ObjectiveEvaluation);
        Assert.Contains(evaluation.EvidenceRequests, request => request.Candidate == "Birch 15");
        Assert.Contains(evaluation.EvidenceRequests, request => request.Candidate == "Cedar 14");
        var aster = Assert.Single(evaluation.Candidates, candidate => candidate.Candidate == "Aster 14");
        var budget = Assert.Single(aster.Conditions, condition => condition.Name == "spending-limit");
        Assert.Equal("Satisfied", budget.Status);
        var source = Assert.Single(budget.Evidence);
        Assert.Equal("ExternalObservation", source.Kind);
        Assert.Equal("https://example.org/aster-14", source.SourceId);
        Assert.Equal(evidence.RetrievedAt, source.RetrievedAt);
        Assert.Equal(evidence.FreshUntil, source.FreshUntil);
        Assert.Equal(evidence.SourceText, source.SourceText);
    }

    [Fact]
    public async Task Rejects_reviewed_candidate_price_when_currency_differs_from_objective_budget()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);
        var pack = await session.LoadKnowledgePackAsync(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "laptop-candidates.json")),
            TestContext.Current.CancellationToken);
        Assert.True(pack.IsSuccess, pack.Error);
        await session.SubmitAsync(
            "I'm looking for a laptop that will last a long time. I don't need gaming performance, but I care about screen quality, reliability, and repairability. Help me compare these three options. My budget is $1,000.",
            TestContext.Current.CancellationToken);

        var result = await session.AddReviewedCandidateMoneyEvidenceAsync(
            new LatticeCandidateMoneyEvidence(
                "Aster 14", 900m, "€", "Aster 14 is listed at €900.",
                "https://example.org/aster-14", DateTimeOffset.UtcNow));

        Assert.False(result.IsSuccess);
        Assert.Contains("currency", result.Error, StringComparison.OrdinalIgnoreCase);
        var status = await session.GetStatusAsync(TestContext.Current.CancellationToken);
        Assert.Contains(Assert.IsType<LatticeObjectiveEvaluation>(status.ObjectiveEvaluation).EvidenceRequests,
            request => request.Candidate == "Aster 14");
    }

    [Fact]
    public async Task Ceramics_course_selection_transfers_generic_objective_evidence_preferences_and_temporal_planning()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);
        var pack = await session.LoadKnowledgePackAsync(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ceramics-course-candidates.json")),
            TestContext.Current.CancellationToken);
        Assert.True(pack.IsSuccess, pack.Error);

        const string request = "Help me choose a Saturday ceramics class. I have a limited budget and no car. I would prefer a small group and beginner-friendly instruction.";
        var initialTurn = await session.SubmitAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(LatticeUtteranceResultKind.ClarificationRequired, Assert.Single(initialTurn.Utterances).Kind);
        Assert.Contains("What is your spending limit?", initialTurn.Utterances.Single().Message);

        const string clarification = "$180 total. I'm starting at Central Station, and I can leave at 9:00 and be back by 13:00.";
        var clarificationTurn = await session.SubmitAsync(clarification, TestContext.Current.CancellationToken);

        var initialStatus = await session.GetStatusAsync(TestContext.Current.CancellationToken);
        Assert.Contains(request, initialStatus.ActiveObjectiveSource ?? string.Empty);
        var initial = Assert.IsType<LatticeObjectiveEvaluation>(initialStatus.ObjectiveEvaluation);
        Assert.Equal(4, initial.Candidates.Length);
        Assert.Equal("Northbank Ceramics", Assert.Single(initial.EvidenceRequests).Candidate);
        Assert.Contains(clarificationTurn.Utterances,
            utterance => utterance.Message.Contains(initial.EvidenceRequests[0].Prompt, StringComparison.Ordinal));
        Assert.Single(
            clarificationTurn.Utterances,
            utterance => utterance.Message.Contains(initial.EvidenceRequests[0].Prompt, StringComparison.Ordinal));
        Assert.DoesNotContain("I'd recommend", clarificationTurn.Utterances[0].Message);

        var clayHouse = Assert.Single(initial.Candidates, candidate => candidate.Candidate == "Clay House Studio");
        Assert.Equal("Eligible", clayHouse.Feasibility);
        Assert.Equal(3, clayHouse.Conditions.SelectMany(condition => condition.TemporalWitness).Count());
        Assert.All(clayHouse.Conditions.SelectMany(condition => condition.Preferences),
            preference => Assert.Equal("Satisfied", preference.Status));

        var community = Assert.Single(initial.Candidates, candidate => candidate.Candidate == "Community Kiln");
        Assert.Equal("Eligible", community.Feasibility);
        var communityPreferences = community.Conditions.SelectMany(condition => condition.Preferences).ToArray();
        Assert.Contains(communityPreferences, preference => preference.Name == "a small group" && preference.Status == "Violated");
        Assert.Contains(communityPreferences, preference => preference.Name == "beginner-friendly instruction" && preference.Status == "Satisfied");
        Assert.Equal(3, community.Conditions.SelectMany(condition => condition.TemporalWitness).Count());

        var northbank = Assert.Single(initial.Candidates, candidate => candidate.Candidate == "Northbank Ceramics");
        Assert.Equal("Unresolved", northbank.Feasibility);
        Assert.Equal(3, northbank.Conditions.SelectMany(condition => condition.TemporalWitness).Count());
        var foundry = Assert.Single(initial.Candidates, candidate => candidate.Candidate == "Foundry Workshop");
        Assert.Equal("Excluded", foundry.Feasibility);
        Assert.True(foundry.HasHardConstraintViolation);

        var answer = await session.SubmitAsync(
            "Northbank Ceramics costs $150.",
            TestContext.Current.CancellationToken);
        Assert.Equal(LatticeUtteranceResultKind.ObjectiveInformationReceived, Assert.Single(answer.Utterances).Kind);
        Assert.Contains("Recorded Northbank Ceramics's total cost as $150.", answer.Utterances.Single().Message);

        var afterAnswer = await session.GetStatusAsync(TestContext.Current.CancellationToken);
        var answered = Assert.IsType<LatticeObjectiveEvaluation>(afterAnswer.ObjectiveEvaluation);
        Assert.Empty(answered.EvidenceRequests);
        var answeredNorthbank = Assert.Single(answered.Candidates, candidate => candidate.Candidate == "Northbank Ceramics");
        Assert.Equal("Eligible", answeredNorthbank.Feasibility);
        var northbankBudget = Assert.Single(answeredNorthbank.Conditions, condition => condition.Name == "spending-limit");
        Assert.Equal("Satisfied", northbankBudget.Status);
        Assert.Equal("UserAssertion", Assert.Single(northbankBudget.Evidence).Kind);

        var revised = await session.SubmitAsync("Walking only.", TestContext.Current.CancellationToken);
        Assert.Equal(LatticeUtteranceResultKind.ObjectiveInformationReceived, Assert.Single(revised.Utterances).Kind);
        var revisedStatus = await session.GetStatusAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Walking only.", revisedStatus.ActiveObjectiveSource ?? string.Empty);
        var final = Assert.IsType<LatticeObjectiveEvaluation>(revisedStatus.ObjectiveEvaluation);
        Assert.Empty(final.EvidenceRequests);
        Assert.Equal("Eligible", Assert.Single(final.Candidates, candidate => candidate.Candidate == "Clay House Studio").Feasibility);
        Assert.Equal("Excluded", Assert.Single(final.Candidates, candidate => candidate.Candidate == "Community Kiln").Feasibility);
        Assert.Equal("Eligible", Assert.Single(final.Candidates, candidate => candidate.Candidate == "Northbank Ceramics").Feasibility);
        Assert.Equal("Excluded", Assert.Single(final.Candidates, candidate => candidate.Candidate == "Foundry Workshop").Feasibility);
        Assert.Equal(
            new[] { "Clay House Studio", "Northbank Ceramics" },
            final.Recommendation.ShortlistedCandidates.OrderBy(name => name, StringComparer.Ordinal));

        var response = revised.Utterances.Single().Message;
        Assert.Contains("You have two good options: Clay House Studio and Northbank Ceramics.", response);
        Assert.Contains("total cost $160", response);
        Assert.Contains("total cost $150", response);
        Assert.Contains("beginner wheel-throwing class", response);
        Assert.Contains("beginner glazing and shaping class", response);
        Assert.Contains("Walk", response);
        response.Should().Contain(
            "Both meet your requirements and match your stated preferences for a small group and beginner-friendly instruction.");
        response.Should().Contain(
            $"{Environment.NewLine}{Environment.NewLine}Note: These times and costs come from the supplied information, not live checks. Verify transport departures and opening hours before leaving. No booking has been made.");
        response.Should().NotContain("Both fit your budget");
        Assert.DoesNotContain("outing", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Candidate assessment", response);
        Assert.DoesNotContain("Evidence:", response);
    }
    [Theory]
    [InlineData("Actually, my budget is $800.")]
    [InlineData("Correction: my budget is $800.")]
    [InlineData("To clarify, my budget is $800.")]
    [InlineData("I meant my budget is $800.")]
    public async Task Clarifies_conflicting_budget_assertions_but_accepts_an_explicit_correction(string correctionText)
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);
        const string request = "I'm looking for a laptop that will last a long time. I don't need gaming performance, but I care about screen quality, reliability, and repairability. Help me compare these three options. My budget is $1,000.";

        await session.SubmitAsync(request, TestContext.Current.CancellationToken);

        var conflict = await session.SubmitAsync(
            "My budget is $900.",
            TestContext.Current.CancellationToken);
        var afterConflict = await session.GetStatusAsync(TestContext.Current.CancellationToken);

        var conflictUtterance = Assert.Single(conflict.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.ClarificationRequired, conflictUtterance.Kind);
        Assert.Contains("conflicts with the spending limit", conflictUtterance.Message);
        Assert.Contains("No objective conditions were changed", conflictUtterance.Message);
        Assert.DoesNotContain("My budget is $900.", afterConflict.ActiveObjectiveSource);

        var correction = await session.SubmitAsync(
            correctionText,
            TestContext.Current.CancellationToken);
        var afterCorrection = await session.GetStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            LatticeUtteranceResultKind.ObjectiveInformationReceived,
            Assert.Single(correction.Utterances).Kind);
        Assert.Contains(correctionText, afterCorrection.ActiveObjectiveSource);
        Assert.DoesNotContain("My budget is $900.", afterCorrection.ActiveObjectiveSource);
        Assert.Contains("hardconstraint(spending-limit, atmost, $800)", correction.Utterances.Single().Meaning);
    }

}
