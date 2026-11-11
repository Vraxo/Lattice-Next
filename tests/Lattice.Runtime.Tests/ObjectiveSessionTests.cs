using Lattice.Client;
using Lattice.Runtime;
using Xunit;

namespace Lattice.Runtime.Tests;

public sealed class ObjectiveSessionTests
{
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
