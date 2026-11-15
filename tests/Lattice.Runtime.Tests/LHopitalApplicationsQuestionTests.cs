using Lattice.Client;
using Lattice.Runtime;
using Xunit;

namespace Lattice.Runtime.Tests;

public sealed class LHopitalApplicationsQuestionTests
{
    [Fact]
    public async Task Explains_rule_conditions_and_indirect_everyday_relevance_with_evidence()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(
            "How can L’Hopital’s Rule be used in day to day life?",
            TestContext.Current.CancellationToken);

        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, answer.Kind);
        Assert.NotEmpty(answer.SupportingEvidence);

        Assert.Contains("zero divided by zero (0/0)", answer.Message);
        Assert.Contains("infinity divided by infinity (∞/∞)", answer.Message);
        Assert.Contains("differentiable on a punctured interval", answer.Message);
        Assert.Contains("derivative of the denominator is nonzero", answer.Message);
        Assert.Contains("quotient of the derivatives", answer.Message);
        Assert.Contains("unlikely to apply the rule directly", answer.Message);
        Assert.Contains("instantaneous speed and acceleration analysis", answer.Message);
        Assert.Contains("marginal cost analysis", answer.Message);
        Assert.Contains("comparing long-term growth rates", answer.Message);
        Assert.Contains("engineering models", answer.Message);
        Assert.Contains("do not require L'Hôpital's Rule", answer.Message);
    }
}
