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
    public void Rejects_empty_unsupported_or_multi_sentence_input(string input)
    {
        var result = parser.Parse(input);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Utterance);
        Assert.NotNull(result.Error);
    }
}
