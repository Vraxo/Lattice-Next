using System.Collections.Immutable;

namespace Lattice.Core.Reasoning;

public static class SamplingParameterCalculator
{
    public static bool TryCalculateTemperatureDistribution(
        IReadOnlyList<double> logits,
        double temperature,
        out ImmutableArray<double> probabilities,
        out string? error)
    {
        probabilities = [];
        error = null;

        if (logits.Count == 0 ||
            !double.IsFinite(temperature) ||
            temperature <= 0 ||
            logits.Any(logit => !double.IsFinite(logit)))
        {
            error = "At least one finite logit and a positive finite temperature are required.";
            return false;
        }

        var scaledLogits = logits.Select(logit => logit / temperature).ToArray();
        var maximum = scaledLogits.Max();
        var exponentials = scaledLogits
            .Select(value => Math.Exp(value - maximum))
            .ToArray();
        var total = exponentials.Sum();

        if (!double.IsFinite(total) || total <= 0)
        {
            error = "The temperature-scaled logits could not be normalized.";
            return false;
        }

        probabilities = exponentials
            .Select(value => value / total)
            .ToImmutableArray();
        return true;
    }

    public static bool TrySelectNucleus(
        IReadOnlyList<double> probabilities,
        double topP,
        out ImmutableArray<int> retainedIndices,
        out ImmutableArray<double> renormalizedProbabilities,
        out string? error)
    {
        retainedIndices = [];
        renormalizedProbabilities = [];
        error = null;

        if (probabilities.Count == 0 ||
            !double.IsFinite(topP) ||
            topP <= 0 ||
            topP > 1 ||
            probabilities.Any(value => !double.IsFinite(value) || value < 0))
        {
            error = "A non-empty probability distribution and a top-p threshold in (0, 1] are required.";
            return false;
        }

        var total = probabilities.Sum();
        if (!double.IsFinite(total) || total <= 0)
        {
            error = "The probability distribution must have a positive finite total.";
            return false;
        }

        var ordered = probabilities
            .Select((probability, index) => (Probability: probability / total, Index: index))
            .OrderByDescending(item => item.Probability)
            .ThenBy(item => item.Index)
            .ToArray();

        var retained = new List<(double Probability, int Index)>();
        var cumulative = 0d;
        foreach (var item in ordered)
        {
            retained.Add(item);
            cumulative += item.Probability;
            if (cumulative >= topP)
            {
                break;
            }
        }

        var retainedTotal = retained.Sum(item => item.Probability);
        if (retainedTotal <= 0)
        {
            error = "The selected nucleus has no probability mass.";
            return false;
        }

        retainedIndices = retained.Select(item => item.Index).ToImmutableArray();
        renormalizedProbabilities = retained
            .Select(item => item.Probability / retainedTotal)
            .ToImmutableArray();
        return true;
    }
}
