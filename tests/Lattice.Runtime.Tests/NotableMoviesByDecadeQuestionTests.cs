using Lattice.Client;
using Lattice.Runtime;
using Xunit;

namespace Lattice.Runtime.Tests;

public sealed class NotableMoviesByDecadeQuestionTests
{
    [Fact]
    public async Task Lists_notable_comedies_with_years_styles_and_semantic_evidence()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(
            "What are some of the best comedy movies of the 1980's?",
            TestContext.Current.CancellationToken);

        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, answer.Kind);
        Assert.NotEmpty(answer.SupportingEvidence);
        Assert.Contains("no single objective ranking", answer.Message);
        Assert.Contains("Airplane! (1980)", answer.Message);
        Assert.Contains("The Blues Brothers (1980)", answer.Message);
        Assert.Contains("Tootsie (1982)", answer.Message);
        Assert.Contains("Trading Places (1983)", answer.Message);
        Assert.Contains("Ghostbusters (1984)", answer.Message);
        Assert.Contains("This Is Spinal Tap (1984)", answer.Message);
        Assert.Contains("Back to the Future (1985)", answer.Message);
        Assert.Contains("Ferris Bueller's Day Off (1986)", answer.Message);
        Assert.Contains("The Princess Bride (1987)", answer.Message);
        Assert.Contains("A Fish Called Wanda (1988)", answer.Message);
        Assert.Contains("The Naked Gun: From the Files of Police Squad! (1988)", answer.Message);
        Assert.Contains("When Harry Met Sally... (1989)", answer.Message);
        Assert.Contains("spoof comedy", answer.Message);
        Assert.Contains("romantic comedy", answer.Message);
    }
}
