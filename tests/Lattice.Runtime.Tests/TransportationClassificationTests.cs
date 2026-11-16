using Xunit;
using Lattice.Client;
using Lattice.Runtime;

namespace Lattice.Runtime.Tests;

public sealed class TransportationClassificationTests
{
    [Fact]
    public async Task Classifies_each_transportation_example_from_semantic_knowledge()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(
            "Which of the following is useful for transportation: a glass of wine, a lamp, a train, an iced cube tray, a plane, a bicycle, an apple and a scooter.",
            TestContext.Current.CancellationToken);

        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, answer.Kind);
        Assert.Equal(
            "ask(classify(thing-for-transportation, categories(useful, not-useful), items(glass-of-wine, lamp, train, iced-cube-tray, plane, bicycle, apple, scooter)))",
            answer.Meaning);
        Assert.Equal(
            "Useful for transportation: Train, plane, bicycle, and scooter. Not useful for transportation: Glass of wine, lamp, ice cube tray, and apple.",
            answer.Message);
        Assert.NotEmpty(answer.SupportingEvidence);
    }
}
