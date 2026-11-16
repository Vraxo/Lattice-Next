namespace Lattice.Core.Reasoning;

public sealed record AxialMechanicalResult(decimal StressPascals, decimal Strain, decimal StiffnessNewtonsPerMeter);

public static class MechanicalQuantityCalculator
{
    public static bool TryCalculateAxialResponse(
        decimal forceNewtons,
        decimal crossSectionAreaSquareMeters,
        decimal youngsModulusPascals,
        decimal lengthMeters,
        out AxialMechanicalResult? result,
        out string? error)
    {
        result = null;
        error = null;
        if (forceNewtons < 0 || crossSectionAreaSquareMeters <= 0 ||
            youngsModulusPascals <= 0 || lengthMeters <= 0)
        {
            error = "Force must be non-negative; area, Young's modulus, and length must be positive.";
            return false;
        }
        try
        {
            var stress = forceNewtons / crossSectionAreaSquareMeters;
            var strain = stress / youngsModulusPascals;
            var stiffness = youngsModulusPascals * crossSectionAreaSquareMeters / lengthMeters;
            result = new AxialMechanicalResult(stress, strain, stiffness);
            return true;
        }
        catch (OverflowException)
        {
            error = "The supplied measurements exceed the supported decimal calculation range.";
            return false;
        }
    }
}
