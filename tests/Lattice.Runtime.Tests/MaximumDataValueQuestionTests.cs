using Xunit;
using Lattice.Client;
using Lattice.Runtime;

namespace Lattice.Runtime.Tests;

public sealed class MaximumDataValueQuestionTests
{
    [Fact]
    public async Task Finds_the_individual_with_the_highest_recorded_olympic_gold_medal_count()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(
            "What individual has won the most Olympic gold medals in the history of the games?",
            TestContext.Current.CancellationToken);

        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, answer.Kind);
        Assert.Equal(
            "ask(world-model-maximum(individual, olympic-gold-medal-count))",
            answer.Meaning);
        Assert.Equal(
            "Michael Phelps has won the most Olympic gold medals (23).",
            answer.Message);
        var evidence = Assert.Single(answer.SupportingEvidence);
        var status = await session.GetStatusAsync(TestContext.Current.CancellationToken);
        Assert.Equal(46, status.SemanticDataAssertionCount);
        Assert.Contains(
            evidence.Sources,
            source => source.SourceId ==
                "core-world-model:data:michael-phelps:olympic-gold-medal-count");
    }
}
