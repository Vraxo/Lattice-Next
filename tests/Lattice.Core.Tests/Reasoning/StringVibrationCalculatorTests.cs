using Lattice.Core.Reasoning;
using Xunit;

namespace Lattice.Core.Tests.Reasoning;

public sealed class StringVibrationCalculatorTests
{
    [Fact]
    public void Calculates_ideal_string_wave_speed_and_harmonic_frequencies()
    {
        var success = StringVibrationCalculator.TryCalculateIdealStringVibration(
            100m, 0.01m, 0.5m, 3, out var result, out var error);

        Assert.True(success, error);
        Assert.NotNull(result);
        Assert.Equal(100m, result.WaveSpeedMetersPerSecond);
        Assert.Equal(100m, result.FundamentalFrequencyHertz);
        Assert.Equal(300m, result.HarmonicFrequencyHertz);
    }

    [Fact]
    public void Calculates_fundamental_when_harmonic_number_is_one()
    {
        var success = StringVibrationCalculator.TryCalculateIdealStringVibration(
            80m, 0.02m, 0.4m, 1, out var result, out var error);

        Assert.True(success, error);
        Assert.NotNull(result);
        Assert.Equal(result.FundamentalFrequencyHertz, result.HarmonicFrequencyHertz);
        Assert.Equal(50m, result.FundamentalFrequencyHertz);
    }

    [Theory]
    [InlineData(0, 0.01, 0.5, 1)]
    [InlineData(100, 0, 0.5, 1)]
    [InlineData(100, 0.01, 0, 1)]
    [InlineData(100, 0.01, 0.5, 0)]
    public void Rejects_non_physical_inputs(
        decimal tension,
        decimal linearMassDensity,
        decimal vibratingLength,
        int harmonicNumber)
    {
        Assert.False(StringVibrationCalculator.TryCalculateIdealStringVibration(
            tension, linearMassDensity, vibratingLength, harmonicNumber, out _, out _));
    }
}
