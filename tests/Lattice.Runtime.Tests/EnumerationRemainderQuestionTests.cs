using Xunit;
using Lattice.Client;
using Lattice.Runtime;

namespace Lattice.Runtime.Tests;

public sealed class EnumerationRemainderQuestionTests
{
    [Fact]
    public async Task Resolves_the_contextually_named_member_as_the_missing_daughter()
    {
        const string prompt =
            "Alice's parents have three daughters: Amy, Jessy, and what’s the name of the third daughter?";

        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(prompt);

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, utterance.Kind);
        Assert.Equal("The third daughter is Alice.", utterance.Message);
        Assert.Equal(
            "ask(enumeration-remainder(alice, daughters, 3, position(3), known(amy, jessy)))",
            utterance.Meaning);
        var evidence = Assert.Single(utterance.SupportingEvidence);
        Assert.Equal(LatticeEvidenceKind.Derived, evidence.Kind);
        Assert.Equal(2, evidence.Premises.Length);
        Assert.Contains(evidence.Premises, premise => premise.Sources.Any(source => source.SourceText == prompt));
        Assert.Contains(evidence.Premises, premise => premise.Sources.Any(source => source.SourceId?.EndsWith(":instance-of:female-person", StringComparison.Ordinal) == true));
    }

    [Fact]
    public async Task Resolves_numeric_counts_and_ordinals_for_a_two_member_group()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Mira's parents have 2 daughters: Lena, and what's the name of the 2nd daughter?");

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.InformationUnavailable, utterance.Kind);
        Assert.Empty(utterance.SupportingEvidence);
        
    }

    [Fact]
    public async Task Resolves_irregular_plural_group_names()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Mira's parents have 2 children: Lena, and what's the name of the 2nd child?");

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.InformationUnavailable, utterance.Kind);
        Assert.Empty(utterance.SupportingEvidence);
        Assert.Contains("enumeration-remainder(mira, children, 2", utterance.Meaning);
    }

    [Fact]
    public async Task Resolves_a_different_regular_plural_group()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Noah's parents have three sons: Liam, Oliver, and what is the name of the third son?");

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.InformationUnavailable, utterance.Kind);
        Assert.Empty(utterance.SupportingEvidence);
    }

    [Theory]
    [InlineData("Alex's parents have three daughters: Amy, Jessy, and what's the name of the third daughter?")]
    [InlineData("Alice's parents have three sons: Amy, Jessy, and what's the name of the third son?")]
    public async Task Requires_foundational_person_type_for_family_set_completion(string prompt)
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(prompt);

        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.InformationUnavailable, answer.Kind);
        Assert.Empty(answer.SupportingEvidence);
    }

    [Fact]
    public async Task Does_not_infer_a_remainder_when_the_count_does_not_match()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Alice's parents have three daughters: Amy, and what’s the name of the third daughter?");

        Assert.Equal(LatticeTurnStatus.Rejected, result.Status);
        Assert.Empty(result.Utterances);
    }

    [Fact]
    public async Task Does_not_infer_a_remainder_when_the_question_names_a_different_group()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Alice's parents have three daughters: Amy, Jessy, and what’s the name of the third son?");

        Assert.Equal(LatticeTurnStatus.Rejected, result.Status);
        Assert.Empty(result.Utterances);
    }

    [Fact]
    public async Task Does_not_infer_a_remainder_from_duplicate_named_members()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Alice's parents have three daughters: Amy, Amy, and what’s the name of the third daughter?");

        Assert.Equal(LatticeTurnStatus.Rejected, result.Status);
        Assert.Empty(result.Utterances);
    }
}
