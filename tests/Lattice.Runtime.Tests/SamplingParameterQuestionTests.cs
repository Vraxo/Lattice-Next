using Lattice.Client;
using Lattice.Runtime;
using Xunit;

namespace Lattice.Runtime.Tests;

public sealed class SamplingParameterQuestionTests
{
    [Fact]
    public async Task Explains_temperature_and_top_p_from_their_sampling_mechanisms()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(
            "What are \"Temperature\" and \"Top-p (nucleus sampling)\" in language model? Can you provide an example of what the changes to these parameters would do?",
            TestContext.Current.CancellationToken);

        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, answer.Kind);
        Assert.Equal(
            "ask(world-model-sampling-parameters(temperature, top-p, in(language-model)))",
            answer.Meaning);
        Assert.Contains("Temperature and top-p (nucleus sampling) are both sampling parameters", answer.Message);
        Assert.Contains("rescales next-token logits by dividing each logit by T", answer.Message);
        Assert.Contains("is applied before softmax", answer.Message);
        Assert.Contains("is applied after softmax", answer.Message);
        Assert.Contains("86.7%, 11.7%, 1.6%", answer.Message);
        Assert.Contains("50.7%, 30.7%, 18.6%", answer.Message);
        Assert.Contains("66.7% and 33.3%", answer.Message);
        Assert.NotEmpty(answer.SupportingEvidence);
    }

    [Fact]
    public async Task Abstains_when_a_requested_sampling_parameter_is_not_modelled()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(
            "What are \"Temperature\" and \"Top-k\" in language model? Can you provide an example of what the changes to these parameters would do?",
            TestContext.Current.CancellationToken);

        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.InformationUnavailable, answer.Kind);
        Assert.Empty(answer.SupportingEvidence);
    }
}
