using Lattice.Client;
using Xunit;

namespace Lattice.Runtime.Tests;

public sealed class TypedRelationSourceQuestionTests
{
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
                source.SourceId == "dutch-artists:relation:johannes-vermeer:painted:girl-with-a-pearl-earring"));
        Assert.Contains(
            answer.SupportingEvidence,
            evidence => evidence.Sources.Any(source =>
                source.SourceId == "dutch-artists:relation:johannes-vermeer:instance-of:dutch-artist"));
    }
}
