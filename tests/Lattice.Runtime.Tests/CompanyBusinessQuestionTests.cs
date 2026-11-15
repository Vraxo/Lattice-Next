using Lattice.Client;
using Lattice.Runtime;
using Xunit;

namespace Lattice.Runtime.Tests;

public sealed class CompanyBusinessQuestionTests
{
    [Fact]
    public async Task Asks_for_business_details_instead_of_inventing_a_company_profile()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(
            "I started a new company named Greystone Inc. What do we do?",
            TestContext.Current.CancellationToken);

        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.InformationUnavailable, answer.Kind);
        Assert.Equal(
            "I don't know what this company does yet. What industry or services does it provide?",
            answer.Message);
        Assert.Empty(answer.SupportingEvidence);
    }
}
