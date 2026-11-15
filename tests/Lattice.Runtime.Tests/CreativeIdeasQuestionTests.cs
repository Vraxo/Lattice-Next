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
        Assert.Contains("Braided fabric tie-back", answer.Message);
        Assert.Contains("Rope and bead tie-back", answer.Message);
        Assert.Contains("Repurposed belt tie-back", answer.Message);
        Assert.Contains("Vintage scarf tie-back", answer.Message);
        Assert.Contains("Wooden dowel tie-back", answer.Message);
        Assert.Contains("reclaimed cotton fabric", answer.Message);
        Assert.Contains("large wooden beads", answer.Message);
        Assert.Contains("wall-mounted cup hook", answer.Message);
        Assert.Contains("mounting the finished dowel horizontally beside the window", answer.Message);
        Assert.Contains("pair of wall-mounted dowel brackets", answer.Message);
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
