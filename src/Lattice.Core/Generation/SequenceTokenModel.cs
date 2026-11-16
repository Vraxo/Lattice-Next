using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace Lattice.Core.Generation;

/// <summary>
/// A frequency observation for a possible next token after a token context.
/// An empty context represents an unconditional token observation.
/// </summary>
public sealed record SequenceTokenObservation(
    ImmutableArray<string> Context,
    string NextToken,
    long Count);

/// <summary>
/// A small, format-agnostic n-gram continuation model.
/// </summary>
public sealed class SequenceTokenModel
{
    private const int MaximumObservations = 250_000;
    private const int MaxSupportedContextLength = 8;
    private const int MaximumTokenLength = 4096;

    private readonly ImmutableDictionary<string, TokenDistribution> distributions;
    private readonly int vocabularySize;
    private readonly double smoothing;

    private SequenceTokenModel(
        ImmutableDictionary<string, TokenDistribution> distributions,
        int vocabularySize,
        int maximumContextLength,
        double smoothing)
    {
        this.distributions = distributions;
        this.vocabularySize = vocabularySize;
        MaximumContextLength = maximumContextLength;
        this.smoothing = smoothing;
    }

    public const string EndToken = "<|lattice.end|>";

    private const int MaximumTrainingTokens = 1_000_000;

    public int MaximumContextLength { get; }

    public static bool TryTrain(
        ImmutableArray<ImmutableArray<string>> sequences,
        out SequenceTokenModel? model,
        out string? error,
        int maximumContextLength = 3,
        double smoothing = 0.1)
    {
        model = null;
        error = null;

        if (sequences.IsDefaultOrEmpty ||
            sequences.Length > MaximumObservations ||
            maximumContextLength is < 0 or > MaxSupportedContextLength ||
            !double.IsFinite(smoothing) ||
            smoothing is <= 0 or > 10)
        {
            error = "A valid non-empty sequence corpus and supported model settings are required.";
            return false;
        }

        var aggregates = new Dictionary<
            (string ContextKey, string NextToken),
            (ImmutableArray<string> Context, long Count)>();
        var totalTokens = 0;

        foreach (var sequence in sequences)
        {
            if (sequence.IsDefaultOrEmpty ||
                sequence.Any(token => !IsValidToken(token) || token == EndToken))
            {
                error = "Training sequences must contain valid, non-empty tokens and must not contain the reserved end token.";
                return false;
            }

            totalTokens += sequence.Length;
            if (totalTokens > MaximumTrainingTokens)
            {
                error = "The training corpus exceeds the supported token limit.";
                return false;
            }

            for (var index = 0; index <= sequence.Length; index++)
            {
                var nextToken = index == sequence.Length
                    ? EndToken
                    : sequence[index];
                var maximumContext = Math.Min(maximumContextLength, index);

                for (var contextLength = 0; contextLength <= maximumContext; contextLength++)
                {
                    var context = contextLength == 0
                        ? ImmutableArray<string>.Empty
                        : sequence
                            .Skip(index - contextLength)
                            .Take(contextLength)
                            .ToImmutableArray();
                    var key = (BuildContextKey(context), nextToken);

                    if (aggregates.TryGetValue(key, out var existing))
                    {
                        try
                        {
                            aggregates[key] = (existing.Context, checked(existing.Count + 1));
                        }
                        catch (OverflowException)
                        {
                            error = "The accumulated training count exceeds the supported range.";
                            return false;
                        }
                    }
                    else
                    {
                        if (aggregates.Count >= MaximumObservations)
                        {
                            error = "The training corpus produces too many distinct token observations.";
                            return false;
                        }

                        aggregates.Add(key, (context, 1));
                    }
                }
            }
        }

        var observations = aggregates.Values
            .Select(item => new SequenceTokenObservation(item.Context, item.NextToken, item.Count))
            .ToImmutableArray();

        return TryCreate(
            observations,
            out model,
            out error,
            maximumContextLength,
            smoothing);
    }

    public static bool TryCreate(
        ImmutableArray<SequenceTokenObservation> observations,
        out SequenceTokenModel? model,
        out string? error,
        int maximumContextLength = 3,
        double smoothing = 0.1)
    {
        model = null;
        error = null;

        if (observations.IsDefaultOrEmpty || observations.Length > MaximumObservations)
        {
            error = "A non-empty token observation collection within the supported size limit is required.";
            return false;
        }

        if (maximumContextLength is < 0 or > MaxSupportedContextLength ||
            !double.IsFinite(smoothing) ||
            smoothing is <= 0 or > 10)
        {
            error = "The token model context length or smoothing value is outside its supported range.";
            return false;
        }

        var mutableDistributions = new Dictionary<string, Dictionary<string, long>>(StringComparer.Ordinal);
        var vocabulary = new HashSet<string>(StringComparer.Ordinal);

        foreach (var observation in observations)
        {
            if (observation is null ||
                observation.Context.IsDefault ||
                observation.Context.Length > maximumContextLength ||
                observation.Context.Any(token => !IsValidToken(token)) ||
                !IsValidToken(observation.NextToken) ||
                observation.Count <= 0)
            {
                error = "Every token observation requires a valid context, next token, and positive count.";
                return false;
            }

            var contextKey = BuildContextKey(observation.Context);
            if (!mutableDistributions.TryGetValue(contextKey, out var nextTokens))
            {
                nextTokens = new Dictionary<string, long>(StringComparer.Ordinal);
                mutableDistributions.Add(contextKey, nextTokens);
            }

            nextTokens.TryGetValue(observation.NextToken, out var existingCount);
            try
            {
                nextTokens[observation.NextToken] = checked(existingCount + observation.Count);
            }
            catch (OverflowException)
            {
                error = "The accumulated token observation count exceeds the supported range.";
                return false;
            }

            vocabulary.Add(observation.NextToken);
        }

        if (vocabulary.Count == 0)
        {
            error = "The token model must contain at least one observed token.";
            return false;
        }

        var distributions = ImmutableDictionary.CreateBuilder<string, TokenDistribution>(StringComparer.Ordinal);
        foreach (var item in mutableDistributions)
        {
            long total = 0;
            try
            {
                foreach (var count in item.Value.Values)
                {
                    total = checked(total + count);
                }
            }
            catch (OverflowException)
            {
                error = "The total token observation count exceeds the supported range.";
                return false;
            }

            distributions.Add(
                item.Key,
                new TokenDistribution(
                    item.Value.ToImmutableDictionary(StringComparer.Ordinal),
                    total));
        }

        model = new SequenceTokenModel(
            distributions.ToImmutable(),
            vocabulary.Count,
            maximumContextLength,
            smoothing);
        return true;
    }

    internal ImmutableArray<string> GetNextTokens(
        ImmutableArray<string> precedingTokens)
    {
        var maximumLength = Math.Min(MaximumContextLength, precedingTokens.Length);

        for (var contextLength = maximumLength; contextLength >= 0; contextLength--)
        {
            var context = contextLength == 0
                ? ImmutableArray<string>.Empty
                : precedingTokens
                    .Skip(precedingTokens.Length - contextLength)
                    .ToImmutableArray();

            if (distributions.TryGetValue(BuildContextKey(context), out var distribution))
            {
                return distribution.Counts
                    .OrderByDescending(item => item.Value)
                    .ThenBy(item => item.Key, StringComparer.Ordinal)
                    .Select(item => item.Key)
                    .ToImmutableArray();
            }
        }

        return ImmutableArray<string>.Empty;
    }

    internal double GetLogScore(
        ImmutableArray<string> precedingTokens,
        string nextToken)
    {
        var maximumLength = Math.Min(MaximumContextLength, precedingTokens.Length);

        for (var contextLength = maximumLength; contextLength >= 0; contextLength--)
        {
            var context = contextLength == 0
                ? ImmutableArray<string>.Empty
                : precedingTokens
                    .Skip(precedingTokens.Length - contextLength)
                    .ToImmutableArray();
            var contextKey = BuildContextKey(context);

            if (!distributions.TryGetValue(contextKey, out var distribution))
            {
                continue;
            }

            distribution.Counts.TryGetValue(nextToken, out var observedCount);
            var numerator = observedCount + smoothing;
            var denominator = distribution.Total + (smoothing * vocabularySize);
            return Math.Log(numerator / denominator);
        }

        // An unseen context contributes no preference; grammar and other constraints
        // remain free to select a continuation until relevant observations exist.
        return 0;
    }

    private static string BuildContextKey(ImmutableArray<string> context)
    {
        var builder = new StringBuilder();
        foreach (var token in context)
        {
            builder.Append(token.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(token);
        }

        return builder.ToString();
    }

    private static bool IsValidToken(string? token) =>
        !string.IsNullOrEmpty(token) &&
        token.Length <= MaximumTokenLength;

    private sealed record TokenDistribution(
        ImmutableDictionary<string, long> Counts,
        long Total);
}
