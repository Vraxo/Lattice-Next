using Lattice.Client;
using Lattice.Runtime;
using Xunit;

namespace Lattice.Runtime.Tests;

public sealed class MissingListItemsClarificationTests
{
    [Fact]
    public async Task Requests_the_missing_food_names_instead_of_guessing()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(
            "Tell me whether or not these foods are on EWG's dirty dozen list",
            TestContext.Current.CancellationToken);

        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.ClarificationRequired, utterance.Kind);
        Assert.Equal(
            "Which foods are you referring to? Please list them, and I'll check each one against EWG's dirty dozen list.",
            utterance.Message);
        Assert.Equal("ask(world-model-clarification(foods, EWG's dirty dozen list))", utterance.Meaning);
    }
}
