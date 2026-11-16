using System.Collections.Immutable;
using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class SequenceGrammarLoaderTests
{
    private const string GrammarJson = """
        {
          "formatVersion": 1,
          "productions": [
            {
              "head": "english-sentence",
              "weight": 0,
              "expansion": [
                { "kind": "terminal", "value": "A " },
                { "kind": "binding", "value": "subject" },
                { "kind": "terminal", "value": " " },
                { "kind": "binding", "value": "predicate" },
                { "kind": "terminal", "value": " " },
                { "kind": "binding", "value": "object" },
                { "kind": "terminal", "value": "." }
              ]
            },
            {
              "head": "python-call-expression",
              "expansion": [
                { "kind": "binding", "value": "callable" },
                { "kind": "terminal", "value": "(" },
                { "kind": "binding", "value": "argument" },
                { "kind": "terminal", "value": ")" }
              ]
            }
          ]
        }
        """;

    [Fact]
    public void Loads_grammar_data_used_by_the_shared_sequence_generator()
    {
        var loaded = SequenceGrammarLoader.TryLoad(
            GrammarJson,
            out var grammar,
            out var error);

        Assert.True(loaded, error);

        var englishRequest = new SequenceGenerationRequest(
            "english-sentence",
            ImmutableDictionary<string, ImmutableArray<string>>.Empty
                .Add("subject", ["piano"])
                .Add("predicate", ["produces"])
                .Add("object", ["sound"]));

        var pythonRequest = new SequenceGenerationRequest(
            "python-call-expression",
            ImmutableDictionary<string, ImmutableArray<string>>.Empty
                .Add("callable", ["print"])
                .Add("argument", ["\"Hello, world!\""]));

        Assert.True(SequenceGenerator.TryGenerate(
            grammar!,
            englishRequest,
            out var english,
            out var englishError), englishError);

        Assert.True(SequenceGenerator.TryGenerate(
            grammar!,
            pythonRequest,
            out var python,
            out var pythonError), pythonError);

        Assert.Equal("A piano produces sound.", english!.Text);
        Assert.Equal("print(\"Hello, world!\")", python!.Text);
    }

    [Theory]
    [InlineData("""{"formatVersion":2,"productions":[]}""")]
    [InlineData("""{"formatVersion":1,"productions":[{"head":"root","expansion":[{"kind":"unknown","value":"x"}]}]}""")]
    [InlineData("""{"formatVersion":1,"productions":[{"head":"root","expansion":[],"unexpected":true}]}""")]
    [InlineData("""{"formatVersion":1,"productions":[{"head":"root","expansion":[{"kind":"terminal","value":""}]}]}""")]
    public void Rejects_unsupported_or_malformed_grammar_documents(string json)
    {
        var loaded = SequenceGrammarLoader.TryLoad(
            json,
            out var grammar,
            out var error);

        Assert.False(loaded);
        Assert.Null(grammar);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}
