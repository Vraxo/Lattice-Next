using Lattice.Client;
using Lattice.Runtime;
using Xunit;

namespace Lattice.Runtime.Tests;

public sealed class CompanyBusinessQuestionTests
{
    [Fact]
    public async Task Suggests_structured_business_directions_without_claiming_the_company_already_does_them()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(
            "I started a new company named Greystone Inc. What do we do?",
            TestContext.Current.CancellationToken);

        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, answer.Kind);
        Assert.StartsWith(
            "Greystone Inc. does not have a defined business in the information provided. Here are three possible directions to consider:",
            answer.Message);
        Assert.Contains("Property maintenance and renovation coordination", answer.Message);
        Assert.Contains("Reclaimed building materials marketplace", answer.Message);
        Assert.Contains("Small-business energy-efficiency retrofits", answer.Message);
        Assert.Contains("These are proposals, not facts about an existing company.", answer.Message);
        Assert.NotEmpty(answer.SupportingEvidence);
    }
}
