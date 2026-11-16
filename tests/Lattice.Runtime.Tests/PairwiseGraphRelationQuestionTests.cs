using Lattice.Client;
using Xunit;

namespace Lattice.Runtime.Tests;

public sealed class PairwiseGraphRelationQuestionTests
{
    [Fact]
    public async Task Answers_a_direct_pairwise_relation_question()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(
            "How is Steve Urkel related to Winslow?",
            TestContext.Current.CancellationToken);

        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, answer.Kind);
        Assert.Equal(
            "ask(relation-between(steve-urkel, winslow))",
            answer.Meaning);
        Assert.Equal(
            "Steve Urkel is next-door neighbor of the Winslow family.",
            answer.Message);
        Assert.Contains(
            answer.SupportingEvidence,
            evidence => evidence.Sources.Any(source =>
                source.SourceId?.Contains(":neighbor-of:", StringComparison.Ordinal) == true));
    }

    [Fact]
    public async Task Answers_a_pairwise_query_through_a_unique_short_graph_path()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(
            "How is Family Matters related to Winslow?",
            TestContext.Current.CancellationToken);

        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, answer.Kind);
        Assert.Equal(
            "The shortest graph path is supported by these facts: Steve Urkel appears in Family Matters. Steve Urkel is next-door neighbor of the Winslow family.",
            answer.Message);
        Assert.Contains(
            answer.SupportingEvidence,
            evidence => evidence.Sources.Any(source =>
                source.SourceId?.Contains(":character-in:", StringComparison.Ordinal) == true));
        Assert.Contains(
            answer.SupportingEvidence,
            evidence => evidence.Sources.Any(source =>
                source.SourceId?.Contains(":neighbor-of:", StringComparison.Ordinal) == true));
    }

    [Fact]
    public async Task Reports_the_asserted_direction_when_the_question_reverses_the_pair()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(
            "How is Winslow related to Steve Urkel?",
            TestContext.Current.CancellationToken);

        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, answer.Kind);
        Assert.Equal(
            "Steve Urkel is next-door neighbor of the Winslow family.",
            answer.Message);
        Assert.Contains(
            answer.SupportingEvidence,
            evidence => evidence.Sources.Any(source =>
                source.SourceId?.Contains(":neighbor-of:", StringComparison.Ordinal) == true));
    }

    [Fact]
    public async Task Abstains_when_the_two_concepts_have_no_short_graph_path()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(
            "How is Steve Urkel related to Mount Whitney?",
            TestContext.Current.CancellationToken);

        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.InformationUnavailable, answer.Kind);
        Assert.Empty(answer.SupportingEvidence);
    }
}
