using Lattice.Client;
using Lattice.Runtime;
using Xunit;

namespace Lattice.Runtime.Tests;

public sealed class RegionalNameQuestionTests
{
    [Fact]
    public async Task Resolves_american_golf_term_and_explains_its_score_with_evidence()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(
            "In golf what do the Americans call an albatross",
            TestContext.Current.CancellationToken);

        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, answer.Kind);
        Assert.Contains("In American English, albatross is called double eagle.", answer.Message);
        Assert.Contains("three under par on a single hole", answer.Message);
        Assert.NotEmpty(answer.SupportingEvidence);
    }
}
