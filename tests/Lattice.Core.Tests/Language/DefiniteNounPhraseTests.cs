using Lattice.Core.Language;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Language;

public sealed class DefiniteNounPhraseTests
{
    private readonly LceParser parser = new();

    [Fact]
    public void Parses_a_definite_description_as_one_structured_relation_argument()
    {
        var result = parser.Parse("Fido likes the red ball.");

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.IsType<SemanticUtterance>(result.Utterance);
        var relation = Assert.IsType<PredicateApplication>(utterance.Meaning);

        Assert.Equal("likes", relation.Predicate.Value);
        Assert.Equal("fido", Assert.IsType<IndividualTerm>(relation.Arguments[0]).Identifier.Value);

        var description = Assert.IsType<DefiniteDescriptionTerm>(relation.Arguments[1]);
        Assert.Equal("ball", description.Head.Value);
        Assert.Equal("red", Assert.Single(description.Modifiers));
        Assert.Equal("likes(fido, the(ball, red))", SemanticFormatter.Format(utterance));
    }

    [Fact]
    public void Parses_multiple_modifiers_as_one_order_independent_description()
    {
        var first = ParseRelation("Fido likes the shiny red ball.");
        var second = ParseRelation("Fido likes the red shiny ball.");

        var firstDescription = Assert.IsType<DefiniteDescriptionTerm>(first.Arguments[1]);
        var secondDescription = Assert.IsType<DefiniteDescriptionTerm>(second.Arguments[1]);

        Assert.Equal(
            new[] { "red", "shiny" },
            firstDescription.Modifiers.Select(modifier => modifier.Value));
        Assert.Equal(
            firstDescription.Modifiers,
            secondDescription.Modifiers);
    }

    [Fact]
    public void Parses_a_matching_question_with_the_same_description_structure()
    {
        var result = parser.Parse("Does Fido like the red ball?");

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.IsType<SemanticUtterance>(result.Utterance);
        Assert.Equal(
            "ask(likes(fido, the(ball, red)))",
            SemanticFormatter.Format(utterance));
    }

    [Fact]
    public void Preserves_existing_bare_noun_relation_arguments_as_individual_terms()
    {
        var result = parser.Parse("Fido likes milk.");

        Assert.True(result.IsSuccess, result.Error);
        var relation = Assert.IsType<PredicateApplication>(result.Utterance!.Meaning);
        Assert.Equal("milk", Assert.IsType<IndividualTerm>(relation.Arguments[1]).Identifier.Value);
    }

    [Fact]
    public void Parses_descriptive_arguments_in_multi_sentence_input()
    {
        var result = parser.ParseText(
            "Fido likes the red ball. Does Fido like the red ball?");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(2, result.Utterances.Length);
        Assert.Equal(
            "likes(fido, the(ball, red))",
            SemanticFormatter.Format(result.Utterances[0]));
        Assert.Equal(
            "ask(likes(fido, the(ball, red)))",
            SemanticFormatter.Format(result.Utterances[1]));
    }

    [Theory]
    [InlineData("Fido likes the.")]
    [InlineData("Does Fido like the?")]
    [InlineData("Fido likes a red ball.")]
    [InlineData("Does Fido like a red ball?")]
    public void Rejects_incomplete_or_unsupported_determiner_phrases(string input)
    {
        Assert.False(parser.Parse(input).IsSuccess);
    }

    private PredicateApplication ParseRelation(string input)
    {
        var result = parser.Parse(input);
        Assert.True(result.IsSuccess, result.Error);
        return Assert.IsType<PredicateApplication>(result.Utterance!.Meaning);
    }
}
