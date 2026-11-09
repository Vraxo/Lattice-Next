using Lattice.Core.Language;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Language;

public sealed class NativeEnglishGrammarTests
{
    private readonly LceParser parser = new();

    [Theory]
    [InlineData("Fido belongs to the class of dogs.", UtteranceKind.Assertion, "dog(fido)")]
    [InlineData("All dogs are animals.", UtteranceKind.Assertion, "forall(x, implies(dog(x), animal(x)))")]
    [InlineData("There is a red ball that is shiny.", UtteranceKind.Assertion, "exists(x, and(red(x), ball(x), shiny(x)))")]
    [InlineData("Milk is something that Fido likes.", UtteranceKind.Assertion, "likes(fido, milk)")]
    [InlineData("All dogs like milk.", UtteranceKind.Assertion, "forall(x, implies(dog(x), likes(x, milk)))")]
    [InlineData("Every dog that is red likes milk.", UtteranceKind.Assertion, "forall(x, implies(and(red(x), dog(x)), likes(x, milk)))")]
    [InlineData("The ball that is red is shiny.", UtteranceKind.Assertion, "shiny(red-ball)")]
    [InlineData("Fido likes the ball that is red.", UtteranceKind.Assertion, "likes(fido, red-ball)")]
    [InlineData("Milk is liked by it.", UtteranceKind.Assertion, "likes(it, milk)")]
    [InlineData("Does Fido count as an animal?", UtteranceKind.Question, "ask(animal(fido))")]
    [InlineData("Is there a red ball?", UtteranceKind.Question, "ask(exists(x, and(red(x), ball(x))))")]
    [InlineData("Is milk liked by Fido?", UtteranceKind.Question, "ask(likes(fido, milk))")]
    [InlineData("Only dogs are animals.", UtteranceKind.Assertion, "forall(x, implies(animal(x), dog(x)))")]
    [InlineData("All red dogs are animals.", UtteranceKind.Assertion, "forall(x, implies(and(red(x), dog(x)), animal(x)))")]
    [InlineData("Every dog that is red is an animal.", UtteranceKind.Assertion, "forall(x, implies(and(red(x), dog(x)), animal(x)))")]
    [InlineData("All dogs that are red are animals.", UtteranceKind.Assertion, "forall(x, implies(and(red(x), dog(x)), animal(x)))")]
    [InlineData("All red dogs that are old like milk.", UtteranceKind.Assertion, "forall(x, implies(and(red(x), old(x), dog(x)), likes(x, milk)))")]
    [InlineData("Some red dogs are animals.", UtteranceKind.Assertion, "exists(x, and(red(x), dog(x), animal(x)))")]
    [InlineData("Some dogs are shiny.", UtteranceKind.Assertion, "exists(x, and(dog(x), shiny(x)))")]
    [InlineData("There is a ball that is red that is shiny.", UtteranceKind.Assertion, "exists(x, and(ball(x), red(x), shiny(x)))")]
    [InlineData("The ball that is red that is shiny is a toy.", UtteranceKind.Assertion, "toy(red-shiny-ball)")]
    [InlineData("The ball that is red likes milk.", UtteranceKind.Assertion, "likes(red-ball, milk)")]
    [InlineData("Fido likes the ball that is red that is shiny.", UtteranceKind.Assertion, "likes(fido, red-shiny-ball)")]
    [InlineData("Does the ball that is red like milk?", UtteranceKind.Question, "ask(likes(red-ball, milk))")]
    [InlineData("Is the ball that is red shiny?", UtteranceKind.Question, "ask(shiny(red-ball))")]
    [InlineData("Milk is liked by Fido.", UtteranceKind.Assertion, "likes(fido, milk)") ]
    public void Parses_supported_english_directly_into_semantics(
        string input,
        UtteranceKind expectedKind,
        string expectedMeaning)
    {
        var result = parser.Parse(input);

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.IsType<SemanticUtterance>(result.Utterance);
        Assert.Equal(expectedKind, utterance.Kind);
        Assert.Equal(input, utterance.SourceText);
        Assert.Equal(expectedMeaning, SemanticFormatter.Format(utterance));
    }

    [Theory]
    [InlineData("No dog is a cat.")]
    [InlineData("Only red dogs are animals.")]
    [InlineData("Some dogs like milk.")]
    [InlineData("Fido does not like milk.")]
    [InlineData("Not every dog likes milk.")]
    [InlineData("Every dog likes some cat.")]
    [InlineData("Every dog that likes milk is friendly.")]
    [InlineData("Mira gave Fido a book.")]
    [InlineData("Could Fido be a dog?")]
    [InlineData("Fido might be a dog.")]
    [InlineData("Does Fido be happy?")]
    [InlineData("I saw her duck.")]
    public void Does_not_guess_at_unsupported_or_ambiguous_meanings(string input)
    {
        var result = parser.Parse(input);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Utterance);
    }

    [Fact]
    public void Parses_mixed_native_english_and_lce_sentences_in_order()
    {
        var result = parser.ParseText(
            "All dogs are animals. Fido is a dog. Is Fido an animal?");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(3, result.Utterances.Length);
        Assert.Equal(
            "forall(x, implies(dog(x), animal(x)))",
            SemanticFormatter.Format(result.Utterances[0]));
        Assert.Equal("dog(fido)", SemanticFormatter.Format(result.Utterances[1]));
        Assert.Equal("ask(animal(fido))", SemanticFormatter.Format(result.Utterances[2]));
    }
}
