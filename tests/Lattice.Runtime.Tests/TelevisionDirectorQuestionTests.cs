using Xunit;
using Lattice.Client;
using Lattice.Runtime;

namespace Lattice.Runtime.Tests;

public sealed class TelevisionDirectorQuestionTests
{
    [Fact]
    public async Task Finds_the_directors_episodes_within_the_requested_season()
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
                source.SourceId.Contains(":directed:", StringComparison.Ordinal)));
        Assert.Contains(
            answer.SupportingEvidence,
            evidence => evidence.Sources.Any(source =>
                source.SourceId.Contains(":includes-episode:", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Does_not_return_episodes_outside_the_requested_scope()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(
            "Which episodes of season three of Game of Thrones did Michelle MacLaren direct?",
            TestContext.Current.CancellationToken);

        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.InformationUnavailable, answer.Kind);
        Assert.Empty(answer.SupportingEvidence);
    }
}
