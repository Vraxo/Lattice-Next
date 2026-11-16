using Lattice.Core.Reasoning;
using Xunit;

namespace Lattice.Core.Tests.Reasoning;

public sealed class SamplingParameterCalculatorTests
{
    [Fact]
    public void Lower_temperature_makes_a_softmax_distribution_more_concentrated()
    {
        var lowSuccess = SamplingParameterCalculator.TryCalculateTemperatureDistribution(
            [2, 1, 0],
            0.5,
            out var low,
            out var lowError);
        var highSuccess = SamplingParameterCalculator.TryCalculateTemperatureDistribution(
            [2, 1, 0],
            2,
            out var high,
            out var highError);

        Assert.True(lowSuccess, lowError);
        Assert.True(highSuccess, highError);
        Assert.True(low[0] > high[0]);
        Assert.InRange(low.Sum(), 0.999999, 1.000001);
        Assert.InRange(high.Sum(), 0.999999, 1.000001);
    }

    [Fact]
    public void Top_p_keeps_the_smallest_sorted_prefix_reaching_the_threshold_and_renormalizes_it()
    {
        var success = SamplingParameterCalculator.TrySelectNucleus(
            [0.6, 0.3, 0.1],
            0.8,
            out var indices,
            out var probabilities,
            out var error);

        Assert.True(success, error);
        Assert.Equal(new[] { 0, 1 }, indices);
        Assert.InRange(probabilities[0], 0.6666, 0.6668);
        Assert.InRange(probabilities[1], 0.3332, 0.3334);
        Assert.InRange(probabilities.Sum(), 0.999999, 1.000001);
    }
}
