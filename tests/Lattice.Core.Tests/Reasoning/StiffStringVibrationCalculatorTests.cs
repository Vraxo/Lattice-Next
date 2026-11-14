using Lattice.Core.Reasoning;
using Xunit;

namespace Lattice.Core.Tests.Reasoning;

public sealed class StiffStringVibrationCalculatorTests
{
    [Fact]
    public void Calculates_stiffness_shifted_fundamental_and_higher_partial()
    {
        var success = StiffStringVibrationCalculator.TryCalculate(
            100m, 0.01m, 0.5m, 200000000000m, 0.000000000001m, 3,
            out var result, out var error);

        Assert.True(success, error);
        Assert.NotNull(result);
        Assert.InRange(result.InharmonicityCoefficient, 0.07895m, 0.07897m);
        Assert.Equal(100m, result.IdealFundamentalFrequencyHertz);
        Assert.InRange(result.FundamentalFrequencyHertz, 103.87m, 103.88m);
        Assert.InRange(result.PartialFrequencyHertz, 392.37m, 392.38m);
        Assert.True(result.PartialFrequencyHertz > 3m * result.FundamentalFrequencyHertz);
    }

    [Fact]
    public void First_partial_equals_stiffness_shifted_fundamental()
    {
        var success = StiffStringVibrationCalculator.TryCalculate(
            100m, 0.01m, 0.5m, 200000000000m, 0.000000000001m, 1,
            out var result, out var error);

        Assert.True(success, error);
        Assert.NotNull(result);
        Assert.Equal(result.FundamentalFrequencyHertz, result.PartialFrequencyHertz);
    }

    [Theory]
    [InlineData(0, 0.01, 0.5, 200000000000, 0.000000000001, 1)]
    [InlineData(100, 0, 0.5, 200000000000, 0.000000000001, 1)]
    [InlineData(100, 0.01, 0, 200000000000, 0.000000000001, 1)]
    [InlineData(100, 0.01, 0.5, 0, 0.000000000001, 1)]
    [InlineData(100, 0.01, 0.5, 200000000000, 0, 1)]
    [InlineData(100, 0.01, 0.5, 200000000000, 0.000000000001, 0)]
    public void Rejects_non_physical_inputs(
        decimal tension,
        decimal linearMassDensity,
        decimal vibratingLength,
        decimal youngsModulus,
        decimal secondMomentOfArea,
        int partialNumber)
    {
        Assert.False(StiffStringVibrationCalculator.TryCalculate(
            tension, linearMassDensity, vibratingLength, youngsModulus,
            secondMomentOfArea, partialNumber, out _, out _));
    }
}
