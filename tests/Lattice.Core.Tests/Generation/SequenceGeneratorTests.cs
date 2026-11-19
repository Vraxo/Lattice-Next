using System.Collections.Immutable;
using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class SequenceGeneratorTests
{
    [Fact]
    public void Uses_the_same_engine_for_natural_language_and_python_sequences()
    {
        var grammar = new SequenceGrammar(ImmutableArray.Create(
            new SequenceProduction(
                "english-sentence",
                ImmutableArray.Create(
                    SequenceSymbol.Token("A"),
                    SequenceSymbol.Token(" "),
                    SequenceSymbol.Bound("subject"),
                    SequenceSymbol.Token(" "),
                    SequenceSymbol.Bound("predicate"),
                    SequenceSymbol.Token(" "),
                    SequenceSymbol.Bound("object"),
                    SequenceSymbol.Token("."))),
            new SequenceProduction(
                "python-statement",
                ImmutableArray.Create(
                    SequenceSymbol.Bound("callable"),
                    SequenceSymbol.Token("("),
                    SequenceSymbol.Bound("argument"),
                    SequenceSymbol.Token(")")))));

        var englishRequest = new SequenceGenerationRequest(
            "english-sentence",
            ImmutableDictionary<string, ImmutableArray<string>>.Empty
                .Add("subject", ["piano"])
                .Add("predicate", ["produces"])
                .Add("object", ["sound"]));

        var pythonRequest = new SequenceGenerationRequest(
            "python-statement",
            ImmutableDictionary<string, ImmutableArray<string>>.Empty
                .Add("callable", ["print"])
                .Add("argument", ["\"Hello, world!\""]));

        Assert.True(SequenceGenerator.TryGenerate(
            grammar,
            englishRequest,
            out var english,
            out var englishError), englishError);

        Assert.True(SequenceGenerator.TryGenerate(
            grammar,
            pythonRequest,
            out var python,
            out var pythonError), pythonError);

        Assert.Equal("A piano produces sound.", english!.Text);
        Assert.Equal("print(\"Hello, world!\")", python!.Text);
        Assert.Equal(
            new[] { "A", " ", "piano", " ", "produces", " ", "sound", "." },
            english.Tokens);
        Assert.Equal(
            new[] { "print", "(", "\"Hello, world!\"", ")" },
            python.Tokens);
    }

    [Fact]
    public void Selects_the_highest_weighted_grammar_continuation()
    {
        var grammar = new SequenceGrammar(ImmutableArray.Create(
            new SequenceProduction(
                "greeting",
                ImmutableArray.Create(
                    SequenceSymbol.Token("Hello "),
                    SequenceSymbol.Bound("name")),
                Weight: 0),
            new SequenceProduction(
                "greeting",
                ImmutableArray.Create(
                    SequenceSymbol.Token("Welcome "),
                    SequenceSymbol.Bound("name")),
                Weight: 1)));

        var request = new SequenceGenerationRequest(
            "greeting",
            ImmutableDictionary<string, ImmutableArray<string>>.Empty
                .Add("name", ["Ada"]));

        var generated = SequenceGenerator.TryGenerate(
            grammar,
            request,
            out var result,
            out var error);

        Assert.True(generated, error);
        Assert.Equal("Welcome Ada", result!.Text);
        Assert.Equal(1, result.Score);
    }

    [Fact]
    public void Uses_contextual_next_token_frequencies_to_choose_a_continuation()
    {
        var grammar = new SequenceGrammar(ImmutableArray.Create(
            new SequenceProduction(
                "phrase",
                ImmutableArray.Create(
                    SequenceSymbol.Token("the "),
                    SequenceSymbol.Symbol("noun"))),
            new SequenceProduction(
                "noun",
                ImmutableArray.Create(SequenceSymbol.Token("cat"))),
            new SequenceProduction(
                "noun",
                ImmutableArray.Create(SequenceSymbol.Token("dog")))));

        Assert.True(SequenceTokenModel.TryCreate(
            ImmutableArray.Create(
                new SequenceTokenObservation(["the "], "cat", 1),
                new SequenceTokenObservation(["the "], "dog", 9)),
            out var tokenModel,
            out var modelError), modelError);

        var request = new SequenceGenerationRequest(
            "phrase",
            ImmutableDictionary<string, ImmutableArray<string>>.Empty,
            TokenModel: tokenModel);

        var generated = SequenceGenerator.TryGenerate(
            grammar,
            request,
            out var result,
            out var error);

        Assert.True(generated, error);
        Assert.Equal("the dog", result!.Text);
        Assert.True(result.Score < 0);
    }

    [Fact]
    public void Continues_a_sequence_from_a_trained_format_agnostic_token_model()
    {
        Assert.True(SequenceTokenModel.TryTrain(
            ImmutableArray.Create(
                ImmutableArray.Create("the ", "cat"),
                ImmutableArray.Create("the ", "cat"),
                ImmutableArray.Create("the ", "dog")),
            out var tokenModel,
            out var modelError,
            maximumContextLength: 1), modelError);

        var generated = SequenceGenerator.TryContinue(
            tokenModel!,
            new SequenceContinuationRequest(
                ["the "],
                MaxTokens: 1),
            out var result,
            out var error);

        Assert.True(generated, error);
        Assert.Equal("cat", result!.Text);
        Assert.DoesNotContain(SequenceTokenModel.EndToken, result.Tokens);
        Assert.False(result.SearchLimitReached);
    }

    [Fact]
    public void Reports_missing_semantic_bindings_instead_of_emitting_placeholders()
    {
        var grammar = new SequenceGrammar(ImmutableArray.Create(
            new SequenceProduction(
                "sentence",
                ImmutableArray.Create(SequenceSymbol.Bound("subject")))));

        var request = new SequenceGenerationRequest(
            "sentence",
            ImmutableDictionary<string, ImmutableArray<string>>.Empty);

        var generated = SequenceGenerator.TryGenerate(
            grammar,
            request,
            out var result,
            out var error);

        Assert.False(generated);
        Assert.Null(result);
        Assert.Contains("subject", error ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_grammars_with_unresolved_nonterminal_symbols()
    {
        var grammar = new SequenceGrammar(ImmutableArray.Create(
            new SequenceProduction(
                "sentence",
                ImmutableArray.Create(SequenceSymbol.Symbol("missing-clause")))));

        var request = new SequenceGenerationRequest(
            "sentence",
            ImmutableDictionary<string, ImmutableArray<string>>.Empty);

        var generated = SequenceGenerator.TryGenerate(
            grammar,
            request,
            out var result,
            out var error);

        Assert.False(generated);
        Assert.Null(result);
        Assert.Contains("missing-clause", error ?? string.Empty, StringComparison.Ordinal);
    }

}
