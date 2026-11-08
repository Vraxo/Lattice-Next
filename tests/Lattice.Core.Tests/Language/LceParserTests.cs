using System.Collections.Immutable;
using Lattice.Core.Language;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Language;

public sealed class LceParserTests
{
    private readonly LceParser parser = new();

    [Fact]
    public void Parses_universal_class_rule_into_language_independent_formula()
    {
        var result = parser.Parse("Every dog is an animal.");

        Assert.True(result.IsSuccess);
        var utterance = Assert.IsType<SemanticUtterance>(result.Utterance);
        Assert.Equal(UtteranceKind.Assertion, utterance.Kind);
        Assert.Equal("Every dog is an animal.", utterance.SourceText);

        var universal = Assert.IsType<UniversalQuantification>(utterance.Meaning);
        Assert.Equal("x", universal.Variable.Identifier.Value);

        var implication = Assert.IsType<Implication>(universal.Body);
        var premise = Assert.IsType<PredicateApplication>(implication.Premise);
        var conclusion = Assert.IsType<PredicateApplication>(implication.Conclusion);

        Assert.Equal("dog", premise.Predicate.Value);
        Assert.Equal("animal", conclusion.Predicate.Value);
        Assert.Equal(universal.Variable, Assert.Single(premise.Arguments));
        Assert.Equal(universal.Variable, Assert.Single(conclusion.Arguments));
    }

    [Fact]
    public void Parses_individual_assertion()
    {
        var result = parser.Parse("Fido is a dog.");

        Assert.True(result.IsSuccess);
        var utterance = Assert.IsType<SemanticUtterance>(result.Utterance);
        Assert.Equal(UtteranceKind.Assertion, utterance.Kind);
        Assert.Equal("dog(fido)", SemanticFormatter.Format(utterance));

        var predicate = Assert.IsType<PredicateApplication>(utterance.Meaning);
        var individual = Assert.IsType<IndividualTerm>(Assert.Single(predicate.Arguments));
        Assert.Equal("fido", individual.Identifier.Value);
    }

    [Fact]
    public void Parses_a_pronoun_assertion_as_an_unresolved_reference()
    {
        var result = parser.Parse("It is a mammal.");

        Assert.True(result.IsSuccess);

        var utterance = Assert.IsType<SemanticUtterance>(result.Utterance);
        Assert.Equal(UtteranceKind.Assertion, utterance.Kind);
        Assert.Equal("mammal(it)", SemanticFormatter.Format(utterance));

        var predicate = Assert.IsType<PredicateApplication>(utterance.Meaning);
        var reference = Assert.IsType<UnresolvedReferenceTerm>(Assert.Single(predicate.Arguments));
        Assert.Equal("it", reference.Reference.Value);
        Assert.Null(utterance.ResolvedReferent);
    }

    [Fact]
    public void Parses_a_pronoun_question_without_resolving_it_during_parsing()
    {
        var result = parser.Parse("Is it a mammal?");

        Assert.True(result.IsSuccess);

        var utterance = Assert.IsType<SemanticUtterance>(result.Utterance);
        Assert.Equal(UtteranceKind.Question, utterance.Kind);
        Assert.Equal("ask(mammal(it))", SemanticFormatter.Format(utterance));
        Assert.IsType<UnresolvedReferenceTerm>(
            Assert.Single(Assert.IsType<PredicateApplication>(utterance.Meaning).Arguments));
    }

    [Fact]
    public void Parses_multiple_sentences_in_order_and_preserves_each_source_sentence()
    {
        var result = parser.ParseText(
            "Every dog is an animal. Fido is a dog. Is Fido an animal?");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(3, result.Utterances.Length);

        Assert.Equal("Every dog is an animal.", result.Utterances[0].SourceText);
        Assert.Equal(UtteranceKind.Assertion, result.Utterances[0].Kind);

        Assert.Equal("Fido is a dog.", result.Utterances[1].SourceText);
        Assert.Equal(UtteranceKind.Assertion, result.Utterances[1].Kind);

        Assert.Equal("Is Fido an animal?", result.Utterances[2].SourceText);
        Assert.Equal(UtteranceKind.Question, result.Utterances[2].Kind);
    }

    [Fact]
    public void Parses_mixed_sentence_terminators_and_inter_sentence_whitespace()
    {
        var result = parser.ParseText(
            "Fido is a dog.  Is Fido a dog?\tEvery dog is an animal.");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(3, result.Utterances.Length);
        Assert.Equal("Fido is a dog.", result.Utterances[0].SourceText);
        Assert.Equal("Is Fido a dog?", result.Utterances[1].SourceText);
        Assert.Equal("Every dog is an animal.", result.Utterances[2].SourceText);
    }

    [Fact]
    public void Rejects_the_entire_text_when_any_sentence_is_invalid()
    {
        var result = parser.ParseText(
            "Fido is a dog. Fido is probably an animal. Is Fido a dog?");

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Utterances);
        Assert.Contains("Sentence 2", result.Error);
    }

    [Fact]
    public void Rejects_an_unterminated_final_sentence()
    {
        var result = parser.ParseText("Fido is a dog. Is Fido an animal");

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Utterances);
        Assert.Contains("Sentence 2", result.Error);
    }

    [Fact]
    public void Rejects_sentences_without_intervening_whitespace()
    {
        var result = parser.ParseText("Fido is a dog.Is Fido a dog?");

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Utterances);
        Assert.Contains("whitespace", result.Error);
    }

    [Fact]
    public void Single_sentence_parse_does_not_silently_accept_a_text()
    {
        var result = parser.Parse("Fido is a dog. Is Fido a dog?");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Utterance);
    }

    [Fact]
    public void Parses_yes_no_question_without_turning_it_into_an_assertion()
    {
        var result = parser.Parse("Is Fido an animal.");

        Assert.False(result.IsSuccess);

        result = parser.Parse("Is Fido an animal?");

        Assert.True(result.IsSuccess);
        var utterance = Assert.IsType<SemanticUtterance>(result.Utterance);
        Assert.Equal(UtteranceKind.Question, utterance.Kind);
        Assert.Equal("ask(animal(fido))", SemanticFormatter.Format(utterance));
    }

    [Fact]
    public void Semantic_model_can_represent_a_binary_relation()
    {
        var relation = new PredicateApplication(
            new SemanticIdentifier("likes"),
            ImmutableArray.Create<SemanticTerm>(
                new IndividualTerm(new SemanticIdentifier("fido")),
                new IndividualTerm(new SemanticIdentifier("milk"))));
        var utterance = new SemanticUtterance(UtteranceKind.Assertion, relation, "Fido likes milk.");

        Assert.Equal("likes(fido, milk)", SemanticFormatter.Format(utterance));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Fido is probably an animal.")]
    [InlineData("Every dog is an animal. Fido is a dog.")]
    [InlineData("every dog is an animal.")]
    [InlineData("it is a mammal.")]
    [InlineData("Is It a mammal?")]
    public void Rejects_empty_unsupported_or_multi_sentence_input(string input)
    {
        var result = parser.Parse(input);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Utterance);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Parse_text_rejects_empty_input()
    {
        Assert.False(parser.ParseText(" ").IsSuccess);
    }
}
