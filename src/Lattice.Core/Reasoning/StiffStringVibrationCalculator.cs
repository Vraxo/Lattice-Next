namespace Lattice.Core.Reasoning;

public sealed record StiffStringVibrationResult(
    decimal InharmonicityCoefficient,
    decimal IdealFundamentalFrequencyHertz,
    decimal FundamentalFrequencyHertz,
    decimal PartialFrequencyHertz);

public static class StiffStringVibrationCalculator
{
    private const decimal PiSquared = 9.8696044010893586188344909999m;

    public static bool TryCalculate(
        decimal tensionNewtons,
        decimal linearMassDensityKilogramsPerMeter,
        decimal vibratingLengthMeters,
        decimal youngsModulusPascals,
        decimal secondMomentOfAreaMetersToFourth,
        int partialNumber,
        out StiffStringVibrationResult? result,
        out string? error)
    {
        result = null;
        error = null;
        if (tensionNewtons <= 0 ||
            linearMassDensityKilogramsPerMeter <= 0 ||
            vibratingLengthMeters <= 0 ||
            youngsModulusPascals <= 0 ||
            secondMomentOfAreaMetersToFourth <= 0 ||
            partialNumber < 1)
        {
            error = "Tension, linear mass density, vibrating length, Young's modulus, and second moment of area must be positive; partial number must be at least one.";
            return false;
        }

        try
        {
            var idealWaveSpeed = SquareRoot(tensionNewtons / linearMassDensityKilogramsPerMeter);
            var idealFundamental = idealWaveSpeed / (2m * vibratingLengthMeters);
            var inharmonicity = PiSquared * youngsModulusPascals *
                secondMomentOfAreaMetersToFourth /
                (tensionNewtons * vibratingLengthMeters * vibratingLengthMeters);
            var fundamental = idealFundamental * SquareRoot(1m + inharmonicity);
            var partialFactor = 1m + inharmonicity * partialNumber * partialNumber;
            var partial = partialNumber * idealFundamental * SquareRoot(partialFactor);

            result = new StiffStringVibrationResult(
                inharmonicity,
                idealFundamental,
                fundamental,
                partial);
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
        if (estimate <= 0)
            throw new DivideByZeroException();

        for (var iteration = 0; iteration < 8; iteration++)
            estimate = (estimate + value / estimate) / 2m;

        return estimate;
    }
}
