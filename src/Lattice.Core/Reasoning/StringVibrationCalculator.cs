namespace Lattice.Core.Reasoning;

public sealed record StringVibrationResult(
    decimal WaveSpeedMetersPerSecond,
    decimal FundamentalFrequencyHertz,
    decimal HarmonicFrequencyHertz);

public static class StringVibrationCalculator
{
    public static bool TryCalculateIdealStringVibration(
        decimal tensionNewtons,
        decimal linearMassDensityKilogramsPerMeter,
        decimal vibratingLengthMeters,
        int harmonicNumber,
        out StringVibrationResult? result,
        out string? error)
    {
        result = null;
        error = null;
        if (tensionNewtons <= 0 || linearMassDensityKilogramsPerMeter <= 0 ||
            vibratingLengthMeters <= 0 || harmonicNumber < 1)
        {
            error = "Tension, linear mass density, and vibrating length must be positive; harmonic number must be at least one.";
            return false;
        }

        try
        {
            var waveSpeed = SquareRoot(tensionNewtons / linearMassDensityKilogramsPerMeter);
            var fundamentalFrequency = waveSpeed / (2m * vibratingLengthMeters);
            var harmonicFrequency = fundamentalFrequency * harmonicNumber;
            result = new StringVibrationResult(waveSpeed, fundamentalFrequency, harmonicFrequency);
            return true;
        }
        catch (OverflowException)
        {
            error = "The supplied measurements exceed the supported decimal calculation range.";
            return false;
        }
        catch (DivideByZeroException)
        {
            error = "The supplied measurements are outside the supported calculation range.";
            return false;
        }
    }

    private static decimal SquareRoot(decimal value)
    {
        var estimate = (decimal)Math.Sqrt((double)value);
        for (var iteration = 0; iteration < 8; iteration++)
            estimate = (estimate + value / estimate) / 2m;
        return estimate;
    }
}
