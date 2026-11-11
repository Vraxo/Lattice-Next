using Lattice.Client;
using Lattice.Runtime;
using Xunit;

namespace Lattice.Runtime.Tests;

public sealed class FirstUseBenchmarkBaselineTests
{
    [Theory]
    [InlineData("I have a free Saturday, a limited budget, and no car. I would prefer somewhere quiet and outdoors. Help me put together a good day.", LatticeTurnStatus.Completed, "put together a good day")]
    [InlineData("$25 total. I'll bring lunch. I'm starting at Central Station, and I can leave at 9:00 and be back by 18:00.", LatticeTurnStatus.Rejected, null)]
    [InlineData("Actually, I don't want to take a bus. Walking only.", LatticeTurnStatus.Rejected, null)]
    [InlineData("I'm looking for a laptop that will last a long time. I don't need gaming performance, but I care about screen quality, reliability, and repairability. Help me compare these three options. My budget is $1,000.", LatticeTurnStatus.Completed, "compare these three options")]
    public async Task Records_the_current_first_use_and_transfer_boundary(string input, LatticeTurnStatus expectedStatus, string? expectedGoal)
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(input, TestContext.Current.CancellationToken);

        Assert.Equal(expectedStatus, result.Status);

        if (expectedGoal is null)
        {
            Assert.Empty(result.Utterances);
            Assert.StartsWith("Sentence 1:", result.Error);
            return;
        }

        Assert.Equal(LatticeUtteranceResultKind.ObjectiveReceived, Assert.Single(result.Utterances).Kind);
        var status = await session.GetStatusAsync(TestContext.Current.CancellationToken);
        Assert.Equal(expectedGoal, status.ActiveObjective);
        Assert.Equal(input, status.ActiveObjectiveSource);
        Assert.Equal(0, status.FactCount);
        Assert.Equal(0, status.RuleCount);
    }
}
