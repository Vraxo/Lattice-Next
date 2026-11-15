using Lattice.Client;
using Lattice.Runtime;
using Xunit;

namespace Lattice.Runtime.Tests;

public sealed class CommonIngredientsQuestionTests
{
    [Fact]
    public async Task Lists_common_and_optional_ingredients_with_evidence_and_variation()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(
            "What are common ingredients of a full english breakfast?",
            TestContext.Current.CancellationToken);

        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, answer.Kind);
        Assert.Contains("eggs", answer.Message);
        Assert.Contains("bacon", answer.Message);
        Assert.Contains("sausages", answer.Message);
        Assert.Contains("baked beans", answer.Message);
        Assert.Contains("grilled tomatoes", answer.Message);
        Assert.Contains("mushrooms", answer.Message);
        Assert.Contains("toast", answer.Message);
        Assert.Contains("black pudding", answer.Message);
        Assert.Contains("fried bread", answer.Message);
        Assert.Contains("hash browns", answer.Message);
        Assert.Contains("vary by region and personal preference", answer.Message);
        Assert.NotEmpty(answer.SupportingEvidence);
    }
}
