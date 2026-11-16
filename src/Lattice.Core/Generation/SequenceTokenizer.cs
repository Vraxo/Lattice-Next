using System.Buffers;
using System.Collections.Immutable;
using System.Text;

namespace Lattice.Core.Generation;

/// <summary>
/// A learned pair merge for a universal subword tokenizer.
/// </summary>
public sealed record SequenceTokenMerge(
    string Left,
    string Right,
    string Merged);

/// <summary>
/// A simple byte-independent Unicode subword tokenizer trained on mixed text corpora.
/// It has no language- or output-format-specific rules.
/// </summary>
public sealed class SequenceTokenizer
{
    private const int MaximumDocuments = 100_000;
    private const int MaximumTextLength = 1_000_000;
    private const int MaximumTrainingUnits = 100_000;
    private const int MaximumMerges = 1_024;

    private readonly ImmutableArray<SequenceTokenMerge> merges;

    private SequenceTokenizer(ImmutableArray<SequenceTokenMerge> merges)
    {
        this.merges = merges;
    }

    public int MergeCount => merges.Length;

    public ImmutableArray<SequenceTokenMerge> Merges => merges;

    public static bool TryTrain(
        ImmutableArray<string> corpus,
        out SequenceTokenizer? tokenizer,
        out string? error,
        int maximumMerges = 128,
        int minimumPairFrequency = 2)
    {
        tokenizer = null;
        error = null;

        if (corpus.IsDefaultOrEmpty || corpus.Length > MaximumDocuments)
        {
            error = "A non-empty corpus within the supported document limit is required.";
            return false;
        }

        if (maximumMerges is < 0 or > MaximumMerges ||
            minimumPairFrequency is < 2 or > MaximumTrainingUnits)
        {
            error = "Tokenizer training limits are outside their supported ranges.";
            return false;
        }

        var sequences = new List<List<string>>(corpus.Length);
        var totalUnits = 0;
        var hasContent = false;

        foreach (var text in corpus)
        {
            if (text is null || text.Length > MaximumTextLength || !IsValidUnicode(text))
            {
                error = "Every corpus document must contain valid Unicode and be within the supported text limit.";
                return false;
            }

            var sequence = text.EnumerateRunes()
                .Select(rune => rune.ToString())
                .ToList();

            totalUnits += sequence.Count;
            if (totalUnits > MaximumTrainingUnits)
            {
                error = "The training corpus exceeds the supported Unicode-unit limit.";
                return false;
            }

            hasContent |= sequence.Count > 0;
            sequences.Add(sequence);
        }

        if (!hasContent)
        {
            error = "The corpus must contain at least one Unicode character.";
            return false;
        }

        var learnedMerges = ImmutableArray.CreateBuilder<SequenceTokenMerge>();

        for (var mergeIndex = 0; mergeIndex < maximumMerges; mergeIndex++)
        {
            var pairCounts = new Dictionary<(string Left, string Right), long>();

            foreach (var sequence in sequences)
            {
                for (var index = 0; index < sequence.Count - 1; index++)
                {
                    var pair = (sequence[index], sequence[index + 1]);
                    pairCounts.TryGetValue(pair, out var count);
                    pairCounts[pair] = count + 1;
                }
            }

            if (pairCounts.Count == 0)
            {
                break;
            }

            var bestPair = pairCounts
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key.Left, StringComparer.Ordinal)
                .ThenBy(pair => pair.Key.Right, StringComparer.Ordinal)
                .First();

            if (bestPair.Value < minimumPairFrequency)
            {
                break;
            }

            var merged = bestPair.Key.Left + bestPair.Key.Right;
            learnedMerges.Add(new SequenceTokenMerge(
                bestPair.Key.Left,
                bestPair.Key.Right,
                merged));

            for (var sequenceIndex = 0; sequenceIndex < sequences.Count; sequenceIndex++)
            {
                var source = sequences[sequenceIndex];
                var destination = new List<string>(source.Count);
                var index = 0;

                while (index < source.Count)
                {
                    if (index + 1 < source.Count &&
                        source[index] == bestPair.Key.Left &&
                        source[index + 1] == bestPair.Key.Right)
                    {
                        destination.Add(merged);
                        index += 2;
                    }
                    else
                    {
                        destination.Add(source[index]);
                        index++;
                    }
                }

                sequences[sequenceIndex] = destination;
            }
        }

        tokenizer = new SequenceTokenizer(learnedMerges.ToImmutable());
        return true;
    }

    public bool TryTokenize(
        string? text,
        out ImmutableArray<string> tokens,
        out string? error)
    {
        tokens = ImmutableArray<string>.Empty;
        error = null;

        if (text is null || text.Length > MaximumTextLength || !IsValidUnicode(text))
        {
            error = "A valid Unicode input within the supported text limit is required.";
            return false;
        }

        var current = text.EnumerateRunes()
            .Select(rune => rune.ToString())
            .ToList();

        foreach (var merge in merges)
        {
            var next = new List<string>(current.Count);
            var index = 0;

            while (index < current.Count)
            {
                if (index + 1 < current.Count &&
                    current[index] == merge.Left &&
                    current[index + 1] == merge.Right)
                {
                    next.Add(merge.Merged);
                    index += 2;
                }
                else
                {
                    next.Add(current[index]);
                    index++;
                }
            }

            current = next;
        }

        tokens = current.ToImmutableArray();
        return true;
    }

    private static bool IsValidUnicode(string text)
    {
        var index = 0;

        while (index < text.Length)
        {
            var status = Rune.DecodeFromUtf16(
                text.AsSpan(index),
                out _,
                out var charsConsumed);
            if (status != OperationStatus.Done)
            {
                return false;
            }

            index += charsConsumed;
        }

        return true;
    }

    public static bool TryDecode(
        ImmutableArray<string> tokens,
        out string text,
        out string? error)
    {
        text = string.Empty;
        error = null;

        if (tokens.IsDefault || tokens.Any(string.IsNullOrEmpty))
        {
            error = "An initialized token sequence with non-empty tokens is required.";
            return false;
        }

        text = string.Concat(tokens);
        return true;
    }
}
