using Xunit;
using Lattice.Client;
using Lattice.Runtime;

namespace Lattice.Runtime.Tests;

public sealed class EnumerationRemainderQuestionTests
{
    [Fact]
    public async Task Resolves_the_contextually_named_member_as_the_missing_daughter()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Alice's parents have three daughters: Amy, Jessy, and what’s the name of the third daughter?");

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, utterance.Kind);
        Assert.Equal("The third daughter is Alice.", utterance.Message);
        Assert.Equal(
            "ask(enumeration-remainder(alice, daughters, 3, position(3), known(amy, jessy)))",
            utterance.Meaning);
        Assert.Empty(utterance.SupportingEvidence);
    }

    [Fact]
    public async Task Resolves_numeric_counts_and_ordinals_in_the_same_construction()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Mira's parents have 2 daughters: Lena, and what's the name of the 2nd daughter?");

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, utterance.Kind);
        Assert.Equal("The 2nd daughter is Mira.", utterance.Message);
        Assert.Equal(
            "ask(enumeration-remainder(mira, daughters, 2, position(2), known(lena)))",
            utterance.Meaning);
    }

    [Fact]
    public async Task Does_not_infer_a_remainder_when_the_count_does_not_match()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Alice's parents have three daughters: Amy, and what’s the name of the third daughter?");

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.InformationUnavailable, utterance.Kind);
    }
}
