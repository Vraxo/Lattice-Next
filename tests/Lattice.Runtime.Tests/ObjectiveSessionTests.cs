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
