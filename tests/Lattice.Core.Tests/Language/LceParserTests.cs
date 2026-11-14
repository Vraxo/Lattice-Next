using System.Collections.Immutable;
using Lattice.Core.Language;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Language;

public sealed class LceParserTests
{
    private readonly LceParser parser = new();

    [Fact]
    public void Parses_candidate_cost_information_without_treating_it_as_a_general_fact()
    {
        var result = parser.Parse("Aster 14 costs $900.");

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.IsType<SemanticUtterance>(result.Utterance);
        Assert.Equal(UtteranceKind.ObjectiveCandidateInformation, utterance.Kind);
        Assert.Equal(
            "candidate-information(aster 14, total-cost, money($900))",
            SemanticFormatter.Format(utterance));
        var information = Assert.IsType<ObjectiveCandidateInformationFormula>(utterance.Meaning);
        Assert.Equal("aster 14", information.Candidate.Value);
        Assert.Equal("total-cost", information.Attribute.Value);
        Assert.Equal(new ObjectiveMoneyValue(900m, "$"), information.Value);
        Assert.Equal("Aster 14 costs $900.", utterance.SourceText);
    }

    [Fact]
    public void Parses_candidate_text_attribute_information()
    {
        var result = parser.Parse("Quiet Park's transport mode is walking.");

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.IsType<SemanticUtterance>(result.Utterance);
        Assert.Equal(UtteranceKind.ObjectiveCandidateInformation, utterance.Kind);
        Assert.Equal(
            "candidate-information(quiet park, transport-mode, text(walking))",
            SemanticFormatter.Format(utterance));
        var information = Assert.IsType<ObjectiveCandidateInformationFormula>(utterance.Meaning);
        Assert.Equal("quiet park", information.Candidate.Value);
        Assert.Equal("transport-mode", information.Attribute.Value);
        Assert.Equal(new ObjectiveTextValue("walking"), information.Value);
        Assert.Equal("Quiet Park's transport mode is walking.", utterance.SourceText);
    }

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
    public void Parses_a_universal_class_rule_with_conjunctive_description_conditions()
    {
        var result = parser.Parse("Every red ball is a toy.");

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.IsType<SemanticUtterance>(result.Utterance);
        var universal = Assert.IsType<UniversalQuantification>(utterance.Meaning);
        var implication = Assert.IsType<Implication>(universal.Body);
        var conjunction = Assert.IsType<Conjunction>(implication.Premise);

        Assert.Equal(
            new[] { "red", "ball" },
            conjunction.Conjuncts
                .Select(formula => Assert.IsType<PredicateApplication>(formula))
                .Select(predicate => predicate.Predicate.Value));
        Assert.Equal(
            "forall(x, implies(and(red(x), ball(x)), toy(x)))",
            SemanticFormatter.Format(utterance));
    }

    [Fact]
    public void Parses_a_universal_rule_with_multiple_modifiers_and_a_head_noun()
    {
        var result = parser.Parse("Every old blue car is a vehicle.");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(
            "forall(x, implies(and(old(x), blue(x), car(x)), vehicle(x)))",
            SemanticFormatter.Format(result.Utterance!));
    }

    [Fact]
    public void Parses_a_quantified_relational_rule_with_a_concrete_object()
    {
        var result = parser.Parse("Every dog likes milk.");

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.IsType<SemanticUtterance>(result.Utterance);
        Assert.Equal(UtteranceKind.Assertion, utterance.Kind);

        var universal = Assert.IsType<UniversalQuantification>(utterance.Meaning);
        var implication = Assert.IsType<Implication>(universal.Body);
        var premise = Assert.IsType<PredicateApplication>(implication.Premise);
        var conclusion = Assert.IsType<PredicateApplication>(implication.Conclusion);

        Assert.Equal("dog", premise.Predicate.Value);
        Assert.Equal("likes", conclusion.Predicate.Value);
        Assert.Equal(universal.Variable, conclusion.Arguments[0]);
        Assert.Equal(
            "milk",
            Assert.IsType<IndividualTerm>(conclusion.Arguments[1]).Identifier.Value);
        Assert.Equal(
            "forall(x, implies(dog(x), likes(x, milk)))",
            SemanticFormatter.Format(utterance));
    }

    [Fact]
    public void Parses_a_quantified_relational_rule_with_conjunctive_conditions()
    {
        var result = parser.Parse("Every old blue car likes milk.");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(
            "forall(x, implies(and(old(x), blue(x), car(x)), likes(x, milk)))",
            SemanticFormatter.Format(result.Utterance!));
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
    [InlineData("It likes milk.", UtteranceKind.Assertion, "likes(it, milk)", 0, 1)]
    [InlineData("Fido likes it.", UtteranceKind.Assertion, "likes(fido, it)", 1, 0)]
    [InlineData("Does it like milk?", UtteranceKind.Question, "ask(likes(it, milk))", 0, 1)]
    [InlineData("Does Fido like it?", UtteranceKind.Question, "ask(likes(fido, it))", 1, 0)]
    public void Parses_one_unresolved_reference_in_either_relational_argument(
        string sentence,
        UtteranceKind expectedKind,
        string expectedMeaning,
        int referenceIndex,
        int groundedIndex)
    {
        var result = parser.Parse(sentence);

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.IsType<SemanticUtterance>(result.Utterance);
        Assert.Equal(expectedKind, utterance.Kind);
        Assert.Equal(expectedMeaning, SemanticFormatter.Format(utterance));

        var predicate = Assert.IsType<PredicateApplication>(utterance.Meaning);
        Assert.IsType<UnresolvedReferenceTerm>(predicate.Arguments[referenceIndex]);
        Assert.IsNotType<UnresolvedReferenceTerm>(predicate.Arguments[groundedIndex]);
        Assert.Null(utterance.ResolvedReferent);
    }

    [Theory]
    [InlineData("It likes it.")]
    [InlineData("Does it like it?")]
    public void Rejects_relational_sentences_with_two_unresolved_references(string sentence)
    {
        var result = parser.Parse(sentence);

        Assert.False(result.IsSuccess);
        Assert.Contains("only one 'it' reference", result.Error);
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
    public void Parses_definite_descriptive_noun_phrases_with_stable_identifiers(
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
        var assertionPredicate = Assert.IsType<PredicateApplication>(assertion.Utterance!.Meaning);
        var questionPredicate = Assert.IsType<PredicateApplication>(question.Utterance!.Meaning);

        Assert.Equal(assertionPredicate.Predicate, questionPredicate.Predicate);
        Assert.Equal(2, assertionPredicate.Arguments.Length);
        Assert.Equal(2, questionPredicate.Arguments.Length);
        Assert.Equal(
            "fido",
            Assert.IsType<IndividualTerm>(assertionPredicate.Arguments[0]).Identifier.Value);
        var assertionDescription =
            Assert.IsType<DescriptiveIndividualTerm>(assertionPredicate.Arguments[1]);
        var questionDescription =
            Assert.IsType<DescriptiveIndividualTerm>(questionPredicate.Arguments[1]);

        Assert.Equal("ball", assertionDescription.Head.Value);
        Assert.Equal("red", Assert.Single(assertionDescription.Modifiers).Value);
        Assert.Equal("red-ball", assertionDescription.Identifier.Value);
        Assert.Equal("ball", questionDescription.Head.Value);
        Assert.Equal("red", Assert.Single(questionDescription.Modifiers).Value);
        Assert.Equal("red-ball", questionDescription.Identifier.Value);
        Assert.Equal(
            "fido",
            Assert.IsType<IndividualTerm>(questionPredicate.Arguments[0]).Identifier.Value);
    }


    [Fact]
    public void Parses_descriptive_noun_phrases_in_a_multi_sentence_turn()
    {
        var result = parser.ParseText(
            "The red ball is a toy. Fido likes the red ball. Does Fido like the red ball?");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(3, result.Utterances.Length);
        Assert.Equal("toy(red-ball)", SemanticFormatter.Format(result.Utterances[0]));
        Assert.Equal("likes(fido, red-ball)", SemanticFormatter.Format(result.Utterances[1]));
        Assert.Equal("ask(likes(fido, red-ball))", SemanticFormatter.Format(result.Utterances[2]));
    }


    [Fact]
    public void Represents_a_description_as_a_head_noun_and_ordered_modifiers()
    {
        var result = parser.Parse("The old blue car is a vehicle.");

        Assert.True(result.IsSuccess, result.Error);
        var predicate = Assert.IsType<PredicateApplication>(result.Utterance!.Meaning);
        var description = Assert.IsType<DescriptiveIndividualTerm>(Assert.Single(predicate.Arguments));

        Assert.Equal("car", description.Head.Value);
        Assert.Equal(2, description.Modifiers.Length);
        Assert.Equal("old", description.Modifiers[0].Value);
        Assert.Equal("blue", description.Modifiers[1].Value);
        Assert.Equal("old-blue-car", description.Identifier.Value);
    }

    [Fact]
    public void Parses_a_property_assertion_and_its_question_form()
    {
        var assertion = parser.Parse("The red ball is red.");
        var question = parser.Parse("Is the red ball red?");

        Assert.True(assertion.IsSuccess, assertion.Error);
        Assert.True(question.IsSuccess, question.Error);
        Assert.Equal("red(red-ball)", SemanticFormatter.Format(assertion.Utterance!));
        Assert.Equal("ask(red(red-ball))", SemanticFormatter.Format(question.Utterance!));
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
    public void Parses_an_existential_assertion_and_its_existence_question()
    {
        var assertion = parser.Parse("A red ball exists.");
        var question = parser.Parse("Does a red ball exist?");

        Assert.True(assertion.IsSuccess, assertion.Error);
        Assert.True(question.IsSuccess, question.Error);

        var assertedMeaning = Assert.IsType<ExistentialQuantification>(assertion.Utterance!.Meaning);
        var queriedMeaning = Assert.IsType<ExistentialQuantification>(question.Utterance!.Meaning);
        Assert.Equal("x", queriedMeaning.Variable.Identifier.Value);
        var queriedConjunction = Assert.IsType<Conjunction>(queriedMeaning.Body);
        Assert.Equal(
            new[] { "red", "ball" },
            queriedConjunction.Conjuncts
                .Select(formula => Assert.IsType<PredicateApplication>(formula))
                .Select(predicate => predicate.Predicate.Value));

        Assert.Equal(
            "exists(x, and(red(x), ball(x)))",
            SemanticFormatter.Format(assertion.Utterance!));
        Assert.Equal(
            "ask(exists(x, and(red(x), ball(x))))",
            SemanticFormatter.Format(question.Utterance!));
        Assert.Equal("A red ball exists.", assertion.Utterance!.SourceText);
    }

    [Theory]
    [InlineData(
        "A red ball is shiny.",
        "exists(x, and(red(x), ball(x), shiny(x)))")]
    [InlineData(
        "An old blue car is a vehicle.",
        "exists(x, and(old(x), blue(x), car(x), vehicle(x)))")]
    [InlineData(
        "A dog exists.",
        "exists(x, dog(x))")]
    public void Parses_indefinite_assertions_as_existential_conjunctions(
        string sentence,
        string expectedMeaning)
    {
        var result = parser.Parse(sentence);

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.IsType<SemanticUtterance>(result.Utterance);
        Assert.Equal(UtteranceKind.Assertion, utterance.Kind);
        Assert.Equal(expectedMeaning, SemanticFormatter.Format(utterance));
        Assert.Equal(sentence, utterance.SourceText);
    }

    [Fact]
    public void Parses_existential_sentences_in_a_multi_sentence_turn()
    {
        var result = parser.ParseText("A red ball is shiny. Does a red ball exist?");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(2, result.Utterances.Length);
        Assert.IsType<ExistentialQuantification>(result.Utterances[0].Meaning);
        Assert.Equal(UtteranceKind.Assertion, result.Utterances[0].Kind);
        Assert.IsType<ExistentialQuantification>(result.Utterances[1].Meaning);
        Assert.Equal(UtteranceKind.Question, result.Utterances[1].Kind);
    }

    [Theory]
    [InlineData("A red ball likes Fido.")]
    [InlineData("A red ball exists")]
    [InlineData("Does the red ball exist?")]
    public void Rejects_indefinite_forms_outside_the_supported_existential_grammar(string sentence)
    {
        Assert.False(parser.Parse(sentence).IsSuccess);
    }

    [Fact]
    public void Parse_text_rejects_empty_input()
    {
        Assert.False(parser.ParseText(" ").IsSuccess);
    }
    [Fact]
    public void Parses_relation_target_questions_into_a_semantic_query()
    {
        var result = parser.ParseText("What does the piano action actuate?");

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(UtteranceKind.Question, utterance.Kind);
        var query = Assert.IsType<SemanticRelationQueryFormula>(utterance.Meaning);
        Assert.Equal("piano-action", query.Subject);
        Assert.Equal("actuate", query.Verb);
        Assert.Equal("ask(relation-targets(piano-action, actuate))", SemanticFormatter.Format(utterance));
    }

    [Fact]
    public void Parses_relation_source_questions_into_a_semantic_query()
    {
        var result = parser.ParseText("What strikes the piano strings?");

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(UtteranceKind.Question, utterance.Kind);
        var query = Assert.IsType<SemanticRelationSourceQueryFormula>(utterance.Meaning);
        Assert.Equal("piano-strings", query.Object);
        Assert.Equal("strikes", query.Verb);
        Assert.Equal(
            "ask(relation-sources(piano-strings, strikes))",
            SemanticFormatter.Format(utterance));
    }

}
