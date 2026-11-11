using Lattice.Client;
using Lattice.Runtime;
using Xunit;

namespace Lattice.Runtime.Tests;

public sealed class ObjectiveSessionTests
{
    [Theory]
    [InlineData("I have a free Saturday, a limited budget, and no car. I would prefer somewhere quiet and outdoors. Help me put together a good day.", "put together a good day")]
    [InlineData("I'm looking for a laptop that will last a long time. I don't need gaming performance, but I care about screen quality, reliability, and repairability. Help me compare these three options. My budget is $1,000.", "compare these three options")]
    public async Task Retains_objective_and_original_request_without_adding_it_to_knowledge(string input, string expectedGoal)
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(input);
        var status = await session.GetStatusAsync();

        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.ObjectiveReceived, Assert.Single(result.Utterances).Kind);
        Assert.Equal(expectedGoal, status.ActiveObjective);
        Assert.Equal(input, status.ActiveObjectiveSource);
        Assert.Equal(0, status.FactCount);
        Assert.Equal(0, status.RuleCount);
    }

    [Fact]
    public async Task Retains_objective_when_later_turns_add_knowledge()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        await session.SubmitAsync("I have a free Saturday. Help me put together a good day.");
        await session.SubmitAsync("Fido is a dog.");
        var status = await session.GetStatusAsync();

        Assert.Equal("put together a good day", status.ActiveObjective);
        Assert.Contains("I have a free Saturday.", status.ActiveObjectiveSource);
        Assert.Equal(1, status.FactCount);
    }
}
