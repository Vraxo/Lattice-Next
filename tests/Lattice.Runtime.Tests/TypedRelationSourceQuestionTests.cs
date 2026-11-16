using Lattice.Client;
using Xunit;

namespace Lattice.Runtime.Tests;

public sealed class TypedRelationSourceQuestionTests
{
    [Fact]
    public async Task Filters_typed_inverse_relation_results_by_a_graph_context()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(
            "Which episodes of season four of Game of Thrones did Michelle MacLaren direct?",
            TestContext.Current.CancellationToken);

        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, answer.Kind);
        Assert.Equal(
            "ask(relation-targets(michelle-maclaren, direct, within(season-four-of-game-of-thrones), target-type(episode)))",
            answer.Meaning);
        Assert.Equal(
            "Oathkeeper and First of His Name are television episodes directed by Michelle MacLaren.",
            answer.Message);
        Assert.Contains(
            answer.SupportingEvidence,
            evidence => evidence.Sources.Any(source =>
                source.SourceId.EndsWith(":relation:michelle-maclaren:directed:oathkeeper", StringComparison.Ordinal)));
        Assert.Contains(
            answer.SupportingEvidence,
            evidence => evidence.Sources.Any(source =>
                source.SourceId.EndsWith(":relation:season-four-of-game-of-thrones:includes-episode:oathkeeper", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Does_not_ignore_a_context_constraint_when_no_context_edge_matches()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(
            "Which episodes of Game of Thrones did Michelle MacLaren direct?",
            TestContext.Current.CancellationToken);

        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.InformationUnavailable, answer.Kind);
        Assert.Empty(answer.SupportingEvidence);
    }

    [Fact]
    public async Task Identifies_a_painter_using_relation_and_type_membership()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(
            "Which Dutch artist painted “Girl with a Pearl Earring”?",
            TestContext.Current.CancellationToken);

        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, answer.Kind);
        Assert.Equal(
            "ask(relation-sources(girl-with-a-pearl-earring, painted, subject-type(dutch-artist)))",
            answer.Meaning);
        Assert.Equal(
            "Johannes Vermeer painted “Girl with a Pearl Earring”.",
            answer.Message);
        Assert.Contains(
            answer.SupportingEvidence,
            evidence => evidence.Sources.Any(source =>
                source.SourceId.EndsWith(":relation:johannes-vermeer:painted:girl-with-a-pearl-earring", StringComparison.Ordinal)));
        Assert.Contains(
            answer.SupportingEvidence,
            evidence => evidence.Sources.Any(source =>
                source.SourceId.EndsWith(":relation:johannes-vermeer:instance-of:dutch-artist", StringComparison.Ordinal)));
    }
}
