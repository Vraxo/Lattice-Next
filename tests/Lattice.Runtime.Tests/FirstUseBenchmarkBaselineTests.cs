using Lattice.Client;
using Lattice.Runtime;
using Xunit;

namespace Lattice.Runtime.Tests;

public sealed class FirstUseBenchmarkBaselineTests
{
    [Theory]
    [InlineData("I have a free Saturday, a limited budget, and no car. I would prefer somewhere quiet and outdoors. Help me put together a good day.")]
    [InlineData("$25 total. I'll bring lunch. I'm starting at Central Station, and I can leave at 9:00 and be back by 18:00.")]
    [InlineData("Actually, I don't want to take a bus. Walking only.")]
    [InlineData("I'm looking for a laptop that will last a long time. I don't need gaming performance, but I care about screen quality, reliability, and repairability. Help me compare these three options. My budget is $1,000.")]
    public async Task Rejects_unstructured_first_use_and_transfer_requests(string input)
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(input);

        Assert.Equal(LatticeTurnStatus.Rejected, result.Status);
        Assert.Empty(result.Utterances);
        Assert.Equal(
            "Sentence 1: That sentence is outside LCE 0.9. Use :help to see the supported forms.",
            result.Error);
    }
}
