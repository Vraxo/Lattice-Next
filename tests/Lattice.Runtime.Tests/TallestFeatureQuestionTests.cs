using Lattice.Client;
using Lattice.Runtime;
using Xunit;

namespace Lattice.Runtime.Tests;

public sealed class TallestFeatureQuestionTests
{
    [Fact]
    public async Task Explains_both_contiguous_and_broader_continental_us_interpretations_with_evidence()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(
            "What is the tallest mountain in the continental US?",
            TestContext.Current.CancellationToken);

        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, answer.Kind);
        Assert.NotEmpty(answer.SupportingEvidence);
        Assert.Contains("contiguous 48 states", answer.Message);
        Assert.Contains("Mount Whitney", answer.Message);
        Assert.Contains("14,505 feet above sea level", answer.Message);
        Assert.Contains("including Alaska", answer.Message);
        Assert.Contains("Denali", answer.Message);
        Assert.Contains("20,320 feet above sea level", answer.Message);
    }
}
