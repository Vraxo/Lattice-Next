using Lattice.Client;
using Lattice.Runtime;
using Xunit;

namespace Lattice.Runtime.Tests;

public sealed class LaptopObjectiveResponseTests
{
    [Fact]
    public async Task Summarizes_known_and_unknown_laptop_evidence_without_inventing_a_winner()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);
        var loaded = await session.LoadKnowledgePackAsync(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "laptop-candidates.json")),
            TestContext.Current.CancellationToken);
        Assert.True(loaded.IsSuccess, loaded.Error);

        const string request = "I'm looking for a laptop that will last a long time. I don't need gaming performance, but I care about screen quality, reliability, and repairability. Help me compare these three options. My budget is $1,000.";
        var result = await session.SubmitAsync(request, TestContext.Current.CancellationToken);
        var message = Assert.Single(result.Utterances).Message;

        Assert.Contains("I can't recommend an option yet.", message);
        Assert.Contains("Current comparison:", message);
        Assert.Contains("Aster 14 — total cost unknown", message);
        Assert.Contains("Birch 15 — total cost unknown", message);
        Assert.Contains("Cedar 14 — total cost unknown", message);
        Assert.Contains("screen quality: unknown", message);
        Assert.Contains("reliability: unknown", message);
        Assert.Contains("repairability: unknown", message);
        Assert.Contains("repairability: supported (Cedar 14 has an upgrade path)", message);
        Assert.Contains("What is the total cost for Aster 14?", message);

        Assert.DoesNotContain("I'd recommend", message);
        Assert.DoesNotContain("unreliable", message);
        Assert.DoesNotContain("poor screen", message);
        Assert.DoesNotContain("warranty", message);
        Assert.DoesNotContain("Candidate assessment", message);
        Assert.DoesNotContain("Evidence:", message);
    }
}
