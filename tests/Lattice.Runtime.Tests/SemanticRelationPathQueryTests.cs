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
            "How are the piano hammers connected to the piano soundboard?");

        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, answer.Kind);
        Assert.Contains("piano hammers", answer.Message);
        Assert.Contains("piano soundboard", answer.Message);
        Assert.Contains("strikes", answer.Message);
        Assert.Contains(
            answer.SupportingEvidence,
            evidence => evidence.Sources.Any(source =>
                source.SourceId.EndsWith(
                    ":relation:piano-hammers:strikes:piano-strings",
                    StringComparison.Ordinal)));
        Assert.Contains(
            answer.SupportingEvidence,
            evidence => evidence.Sources.Any(source =>
                source.SourceId.Contains(
                    ":relation:piano-strings:transmits-vibration-to:piano-soundboard",
                    StringComparison.Ordinal) ||
                source.SourceId.Contains(
                    ":relation:piano-soundboard:receives-vibration-from:piano-strings",
                    StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Does_not_invent_a_path_when_the_graph_has_no_connection()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "How are the piano hammers connected to the golf course?");

        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.InformationUnavailable, answer.Kind);
        Assert.Empty(answer.SupportingEvidence);
    }
}
