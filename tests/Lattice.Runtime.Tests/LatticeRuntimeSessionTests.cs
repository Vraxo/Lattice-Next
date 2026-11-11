using Lattice.Client;
using Lattice.Runtime;

namespace Lattice.Runtime.Tests;

public sealed class LatticeRuntimeSessionTests
{
    [Fact]
    public async Task Stores_assertions_and_answers_questions_with_evidence()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Fido is a dog. Is Fido a dog?");

        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(2, result.Utterances.Length);
        Assert.Equal(
            LatticeUtteranceResultKind.StatementStored,
            result.Utterances[0].Kind);
        Assert.Equal(
            LatticeUtteranceResultKind.QuestionAnswered,
            result.Utterances[1].Kind);
        Assert.Equal(
            LatticeTruthValue.True,
            result.Utterances[1].TruthValue);
        Assert.Contains(
            result.Utterances[1].SupportingEvidence,
            evidence => evidence.Sources.Any(
                source => source.SourceText == "Fido is a dog."));
    }

    [Fact]
    public async Task Keeps_knowledge_isolated_between_sessions()
    {
        var client = new LatticeRuntimeClient();
        await using var first = await client.CreateSessionAsync();
        await using var second = await client.CreateSessionAsync();

        await first.SubmitAsync("Fido is a dog.");
        var result = await second.SubmitAsync("Is Fido a dog?");

        Assert.Equal(LatticeTruthValue.Unknown, result.Utterances[0].TruthValue);
        Assert.NotEqual(first.SessionId, second.SessionId);
    }

    [Fact]
    public async Task Requests_clarification_for_a_missing_referent()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync("Is it a dog?");

        Assert.Equal(
            LatticeUtteranceResultKind.ClarificationRequired,
            result.Utterances[0].Kind);
        Assert.Equal(
            LatticeReferenceStatus.MissingReferent,
            result.Utterances[0].ReferenceStatus);
    }

    [Fact]
    public async Task Rejects_a_turn_before_mutating_session_knowledge_when_parsing_fails()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var rejected = await session.SubmitAsync(
            "Fido is a dog. asdf");
        var followUp = await session.SubmitAsync("Is Fido a dog?");

        Assert.Equal(LatticeTurnStatus.Rejected, rejected.Status);
        Assert.Equal(LatticeTruthValue.Unknown, followUp.Utterances[0].TruthValue);
    }

    [Fact]
    public async Task Rejects_submissions_after_session_disposal()
    {
        var client = new LatticeRuntimeClient();
        var session = await client.CreateSessionAsync();

        await session.DisposeAsync();
        var result = await session.SubmitAsync("Fido is a dog.");

        Assert.Equal(LatticeTurnStatus.Rejected, result.Status);
        Assert.Equal("The session has been closed.", result.Error);
    }
}
