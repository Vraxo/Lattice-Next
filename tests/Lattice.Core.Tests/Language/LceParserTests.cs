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
    public void Parses_a_relational_assertion_and_its_question_form()
    {
        var assertion = parser.Parse("Fido likes milk.");

        Assert.True(assertion.IsSuccess, assertion.Error);
        var fact = Assert.IsType<SemanticUtterance>(assertion.Utterance);
        Assert.Equal(UtteranceKind.Assertion, fact.Kind);
        Assert.Equal("likes(fido, milk)", SemanticFormatter.Format(fact));

        var question = parser.Parse("Does Fido like milk?");

        Assert.True(question.IsSuccess, question.Error);
        var query = Assert.IsType<SemanticUtterance>(question.Utterance);
        Assert.Equal(UtteranceKind.Question, query.Kind);
        Assert.Equal("ask(likes(fido, milk))", SemanticFormatter.Format(query));
    }

    [Theory]
    [InlineData("watch", "watches", "Rex")]
    [InlineData("carry", "carries", "Rex")]
    [InlineData("have", "has", "milk")]
    [InlineData("do", "does", "tricks")]
    [InlineData("go", "goes", "school")]
    public void Normalizes_regular_and_supported_irregular_relation_verbs(
        string baseVerb,
        string predicate,
        string objectValue)
    {
        var result = parser.Parse($"Does Fido {baseVerb} {objectValue}?");

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.IsType<SemanticUtterance>(result.Utterance);
        Assert.Equal(
            $"ask({predicate}(fido, {objectValue.ToLowerInvariant()}))",
            SemanticFormatter.Format(utterance));
    }

    [Fact]
    public void Parses_relational_sentences_inside_a_multi_sentence_text()
    {
        var result = parser.ParseText("Fido likes milk. Does Fido like milk?");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(2, result.Utterances.Length);
        Assert.Equal(UtteranceKind.Assertion, result.Utterances[0].Kind);
        Assert.Equal(UtteranceKind.Question, result.Utterances[1].Kind);
    }

    [Fact]
    public void Parses_a_sentence_after_normalizing_formatting_artifacts()
    {
        var input = "\uFEFFFido\u00A0is\u200Ba\u00A0dog.";
        var result = parser.Parse(input);

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.IsType<SemanticUtterance>(result.Utterance);
        Assert.Equal("dog(fido)", SemanticFormatter.Format(utterance));
        Assert.Equal(input.Trim(), utterance.SourceText);
    }

    [Fact]
    public void Parses_multi_sentence_input_with_an_invisible_separator_and_preserves_source()
    {
        const string firstSentence = "Fido is a dog.";
        const string secondSentence = "Does Fido like milk?";
        var input = $"{firstSentence}\u200B{secondSentence}";

        var result = parser.ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(2, result.Utterances.Length);
        Assert.Equal(firstSentence, result.Utterances[0].SourceText);
        Assert.Equal(secondSentence, result.Utterances[1].SourceText);
    }

    [Fact]
    public void Does_not_silently_repair_missing_grammar_or_punctuation()
    {
        Assert.False(parser.Parse("Fido are a dog.").IsSuccess);
        Assert.False(parser.Parse("Fido is dog.").IsSuccess);
        Assert.False(parser.Parse("Fido is a dog").IsSuccess);
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
    [InlineData("Fido is Rex.")]
    [InlineData("Does Fido be happy?")]
    public void Rejects_empty_unsupported_or_multi_sentence_input(string input)
    {
        var result = parser.Parse(input);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Utterance);
        Assert.NotNull(result.Error);
    }


    [Theory]
    [InlineData("Fido likes the red ball.", "likes(fido, red-ball)")]
    [InlineData("The red ball is a toy.", "toy(red-ball)")]
    [InlineData("The old blue car is a vehicle.", "vehicle(old-blue-car)")]
    public void Parses_definite_descriptive_noun_phrases_as_individual_terms(
        string sentence,
        string expectedMeaning)
    {
        var result = parser.Parse(sentence);

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.IsType<SemanticUtterance>(result.Utterance);
        Assert.Equal(expectedMeaning, SemanticFormatter.Format(utterance));
        Assert.Equal(sentence, utterance.SourceText);
    }

    [Fact]
    public void Parses_a_descriptive_noun_phrase_in_a_relational_question()
    {
        var result = parser.Parse("Does Fido like the red ball?");

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.IsType<SemanticUtterance>(result.Utterance);
        Assert.Equal("ask(likes(fido, red-ball))", SemanticFormatter.Format(utterance));
    }

    [Fact]
    public void Uses_the_same_canonical_identifier_for_repeated_descriptive_noun_phrases()
    {
        var assertion = parser.Parse("Fido likes the red ball.");
        var question = parser.Parse("Does Fido like the red ball?");

        Assert.True(assertion.IsSuccess, assertion.Error);
        Assert.True(question.IsSuccess, question.Error);
        Assert.Equal(
            Assert.IsType<PredicateApplication>(assertion.Utterance!.Meaning),
            Assert.IsType<PredicateApplication>(question.Utterance!.Meaning));
    }

    [Theory]
    [InlineData("Fido likes red ball.")]
    [InlineData("Fido likes the.")]
    [InlineData("Fido likes a red ball.")]
    [InlineData("Is the red ball a toy.")]
    public void Does_not_guess_or_rewrite_unsupported_noun_phrase_forms(string sentence)
    {
        Assert.False(parser.Parse(sentence).IsSuccess);
    }

    [Fact]
    public void Parse_text_rejects_empty_input()
    {
        Assert.False(parser.ParseText(" ").IsSuccess);
    }
}
