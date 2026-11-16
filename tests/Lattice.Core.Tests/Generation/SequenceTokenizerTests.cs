using System.Collections.Immutable;
using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class SequenceTokenizerTests
{
    [Fact]
    public void Uses_one_tokenizer_for_natural_language_python_and_unicode_text()
    {
        var corpus = ImmutableArray.Create(
            "The piano produces sound.",
            "The piano has strings and hammers.",
            "print(\"Hello, world!\")",
            "for obj in objects:\n    print(obj.name)",
            "A musical instrument: π");

        Assert.True(SequenceTokenizer.TryTrain(
            corpus,
            out var tokenizer,
            out var trainingError,
            maximumMerges: 64), trainingError);

        Assert.NotNull(tokenizer);
        Assert.True(tokenizer!.MergeCount > 0);

        foreach (var document in corpus)
        {
            Assert.True(tokenizer.TryTokenize(
                document,
                out var tokens,
                out var tokenizationError), tokenizationError);

            Assert.True(SequenceTokenizer.TryDecode(
                tokens,
                out var decoded,
                out var decodingError), decodingError);

            Assert.Equal(document, decoded);
        }

        Assert.True(tokenizer.TryTokenize(
            "print(\"Hello, world!\")",
            out var firstEncoding,
            out var firstError), firstError);
        Assert.True(tokenizer.TryTokenize(
            "print(\"Hello, world!\")",
            out var secondEncoding,
            out var secondError), secondError);
        Assert.Equal(firstEncoding, secondEncoding);
    }

    [Fact]
    public void Learns_deterministic_merges_from_the_same_corpus()
    {
        var corpus = ImmutableArray.Create(
            "abababab",
            "abababab",
            "abababab");

        Assert.True(SequenceTokenizer.TryTrain(
            corpus,
            out var first,
            out var firstError,
            maximumMerges: 8), firstError);
        Assert.True(SequenceTokenizer.TryTrain(
            corpus,
            out var second,
            out var secondError,
            maximumMerges: 8), secondError);

        Assert.Equal(first!.Merges, second!.Merges);
    }

    [Fact]
    public void Rejects_invalid_training_limits_and_uninitialized_token_sequences()
    {
        var trained = SequenceTokenizer.TryTrain(
            ["text"],
            out var tokenizer,
            out var trainingError,
            maximumMerges: -1);

        Assert.False(trained);
        Assert.Null(tokenizer);
        Assert.False(string.IsNullOrWhiteSpace(trainingError));

        var decoded = SequenceTokenizer.TryDecode(
            default,
            out var text,
            out var decodingError);

        Assert.False(decoded);
        Assert.Equal(string.Empty, text);
        Assert.False(string.IsNullOrWhiteSpace(decodingError));
    }
}
