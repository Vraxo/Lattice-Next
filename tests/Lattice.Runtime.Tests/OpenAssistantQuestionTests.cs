using Lattice.Client;
using Lattice.Runtime;
using Xunit;

namespace Lattice.Runtime.Tests;

public sealed class OpenAssistantQuestionTests
{
    [Fact]
    public async Task Answers_all_three_parts_with_evidence_and_current_project_limitations()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(
            "What is the difference between open assistant and ChatGPT? Why should i use Open Assistant? And can you give me some examples of advanced chatbots that are similar to Open Assistant?",
            TestContext.Current.CancellationToken);

        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(3, result.Utterances.Length);

        Assert.All(result.Utterances, utterance =>
        {
            Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, utterance.Kind);
            Assert.NotEmpty(utterance.SupportingEvidence);
        });

        Assert.Contains("concluded research project", result.Utterances[0].Message);
        Assert.Contains("active hosted AI service", result.Utterances[0].Message);
        Assert.Contains("downloadable model access", result.Utterances[0].Message);
        Assert.Contains("proprietary hosted model-and-service offering", result.Utterances[0].Message);

        Assert.Contains("released model weights", result.Utterances[1].Message);
        Assert.Contains("public demo is no longer available", result.Utterances[1].Message);
        Assert.Contains("Apache 2.0", result.Utterances[1].Message);
        Assert.Contains("base-model-specific weight licenses", result.Utterances[1].Message);
        Assert.Contains("self-hosted experimentation", result.Utterances[1].Message);

        Assert.Contains("Claude", result.Utterances[2].Message);
        Assert.Contains("Gemini", result.Utterances[2].Message);
        Assert.Contains("Microsoft Copilot", result.Utterances[2].Message);
        Assert.Contains("HuggingChat", result.Utterances[2].Message);
        Assert.Contains("Perplexity", result.Utterances[2].Message);
    }
}
