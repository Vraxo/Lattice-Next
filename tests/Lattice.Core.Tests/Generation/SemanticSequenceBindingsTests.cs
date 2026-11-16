using System.Collections.Immutable;
using Lattice.Core.Generation;
using Lattice.Core.Reasoning;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class SemanticSequenceBindingsTests
{
    [Fact]
    public void Generates_a_sentence_from_a_semantic_fact_without_storing_the_sentence()
    {
        var model = CreateModel();
        var assertion = Assert.Single(model.RelationAssertions);

        Assert.True(SemanticSequenceBindings.TryFromAssertion(
            model,
            assertion,
            out var bindings,
            out var bindingError), bindingError);

        var grammar = new SequenceGrammar(ImmutableArray.Create(
            new SequenceProduction(
                "fact",
                ImmutableArray.Create(
                    SequenceSymbol.Token("A"),
                    SequenceSymbol.Token(" "),
                    SequenceSymbol.Bound("subject"),
                    SequenceSymbol.Token(" "),
                    SequenceSymbol.Bound("relation"),
                    SequenceSymbol.Token(" "),
                    SequenceSymbol.Bound("object"),
                    SequenceSymbol.Token(".")))));

        Assert.True(SequenceGenerator.TryGenerate(
            grammar,
            new SequenceGenerationRequest("fact", bindings),
            out var result,
            out var generationError), generationError);

        Assert.Equal("A piano is a musical instrument.", result!.Text);
        Assert.Equal(
            new[] { "A", " ", "piano", " ", "is", " ", "a", " ", "musical", " ", "instrument", "." },
            result.Tokens);
    }

    [Fact]
    public void Rejects_relation_assertions_whose_arguments_have_the_wrong_kinds()
    {
        var invalidAssertion = new SemanticRelationAssertion(
            "musical-instrument",
            "instance-of",
            "piano",
            new SemanticWorldModelSource("test:invalid-fact"));
        var model = CreateModel() with
        {
            RelationAssertions = ImmutableArray.Create(invalidAssertion)
        };

        var resolved = SemanticSequenceBindings.TryFromAssertion(
            model,
            invalidAssertion,
            out var bindings,
            out var error);

        Assert.False(resolved);
        Assert.Empty(bindings);
        Assert.Contains("relation kinds", error ?? string.Empty, StringComparison.Ordinal);
    }

    private static SemanticWorldModel CreateModel()
    {
        var assertion = new SemanticRelationAssertion(
            "piano",
            "instance-of",
            "musical-instrument",
            new SemanticWorldModelSource("test:piano-is-a-musical-instrument"));

        return new SemanticWorldModel(
            4,
            "generation-binding-test",
            "Semantic binding test",
            ImmutableArray.Create(
                new SemanticConcept("piano", "piano", SemanticConceptKind.Individual),
                new SemanticConcept("musical-instrument", "musical instrument", SemanticConceptKind.Type)),
            ImmutableArray<SemanticLexeme>.Empty,
            ImmutableArray.Create(
                new SemanticRelationDefinition(
                    "instance-of",
                    null,
                    null,
                    "is a",
                    null,
                    0,
                    0,
                    SemanticRelationArgumentKind.Individual,
                    SemanticRelationArgumentKind.Type)),
            ImmutableArray<SemanticTypeDefinition>.Empty,
            ImmutableArray.Create(assertion),
            ImmutableArray<SemanticDataPropertyDefinition>.Empty,
            ImmutableArray<SemanticDataAssertion>.Empty);
    }
}
