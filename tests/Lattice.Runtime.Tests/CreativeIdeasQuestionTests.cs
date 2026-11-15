using Xunit;
using Lattice.Client;
using Lattice.Runtime;

namespace Lattice.Runtime.Tests;

public sealed class CreativeIdeasQuestionTests
{
    [Fact]
    public async Task Generates_multiple_diy_ideas_from_materials_methods_and_fasteners()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(
            "What are some unique curtain tie backs that you can make yourself?",
            TestContext.Current.CancellationToken);

        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, answer.Kind);
        Assert.Equal("ask(world-model-creative-ideas(curtain-tie-backs))", answer.Meaning);
        Assert.Contains("braided fabric tie-back", answer.Message);
        Assert.Contains("rope and bead tie-back", answer.Message);
        Assert.Contains("repurposed belt tie-back", answer.Message);
        Assert.Contains("vintage scarf tie-back", answer.Message);
        Assert.Contains("wooden dowel tie-back", answer.Message);
        Assert.Contains("reclaimed cotton fabric", answer.Message);
        Assert.Contains("large wooden beads", answer.Message);
        Assert.Contains("wall-mounted cup hook", answer.Message);
        Assert.NotEmpty(answer.SupportingEvidence);
    }

    [Fact]
    public async Task Does_not_parse_unrelated_questions_as_diy_idea_requests()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(
            "What are some unique curtain tie backs?",
            TestContext.Current.CancellationToken);

        Assert.NotEqual(LatticeTurnStatus.Completed, result.Status);
    }
}
