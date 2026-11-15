using Lattice.Client;
using Lattice.Runtime;
using Xunit;

namespace Lattice.Runtime.Tests;

public sealed class RequiredKnowledgeQuestionTests
{
    [Fact]
    public async Task Explains_entry_requirements_and_progressive_theory_learning_with_evidence()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(
            "How much music theory knowledge is required to compose music?",
            TestContext.Current.CancellationToken);

        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, answer.Kind);
        Assert.NotEmpty(answer.SupportingEvidence);
        Assert.Contains("do not need formal music-theory training", answer.Message);
        Assert.Contains("experimenting by ear", answer.Message);
        Assert.Contains("writing a melody", answer.Message);
        Assert.Contains("basic rhythm", answer.Message);
        Assert.Contains("notes and intervals", answer.Message);
        Assert.Contains("scales and keys", answer.Message);
        Assert.Contains("simple chords and harmony", answer.Message);
        Assert.Contains("advanced harmony", answer.Message);
        Assert.Contains("counterpoint", answer.Message);
        Assert.Contains("orchestration", answer.Message);
        Assert.Contains("tools to learn as needed rather than prerequisites", answer.Message);
    }
}
