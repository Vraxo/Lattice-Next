using Lattice.Client;
using Lattice.Runtime;
using Xunit;

namespace Lattice.Runtime.Tests;

public sealed class SemanticRelationPathQueryTests
{
    [Fact]
    public async Task Explains_a_shortest_relation_path_with_each_assertion_as_evidence()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "How is the piano action connected to the piano strings?");

        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, answer.Kind);
        Assert.Contains(
            "piano action —actuates→ piano hammers —strikes→ piano strings",
            answer.Message);
        Assert.Contains(
            answer.SupportingEvidence,
            evidence => evidence.Sources.Any(source =>
                source.SourceId.EndsWith(
                    ":relation:piano-action:actuates:piano-hammers",
                    StringComparison.Ordinal)));
        Assert.Contains(
            answer.SupportingEvidence,
            evidence => evidence.Sources.Any(source =>
                source.SourceId.EndsWith(
                    ":relation:piano-hammers:strikes:piano-strings",
                    StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Does_not_invent_a_path_when_the_graph_has_no_connection()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "How is the piano action connected to the golf course?");

        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.InformationUnavailable, answer.Kind);
        Assert.Empty(answer.SupportingEvidence);
    }
}
