namespace Lattice.Core.Reasoning;

public static class ChargedParticleAccelerationCalculator
{
    private const double ElementaryChargeCoulombs = 1.602176634e-19;
    private const double AtomicMassUnitKilograms = 1.66053906660e-27;

    public static bool TryCalculateFinalSpeed(
        double initialSpeedMetersPerSecond,
        double potentialDifferenceVolts,
        double chargeInElementaryChargeUnits,
        double massInAtomicMassUnits,
        out ChargedParticleAccelerationResult? result,
        out string? error)
    {
        result = null;
        error = null;

        if (!double.IsFinite(initialSpeedMetersPerSecond) ||
            !double.IsFinite(potentialDifferenceVolts) ||
            !double.IsFinite(chargeInElementaryChargeUnits) ||
            !double.IsFinite(massInAtomicMassUnits) ||
            initialSpeedMetersPerSecond < 0 ||
            massInAtomicMassUnits <= 0)
        {
            error = "Initial speed must be non-negative and particle mass must be positive.";
            return false;
        }

        var massKilograms = massInAtomicMassUnits * AtomicMassUnitKilograms;
        var chargeCoulombs = chargeInElementaryChargeUnits * ElementaryChargeCoulombs;
        var initialKineticEnergyJoules =
            0.5 * massKilograms * initialSpeedMetersPerSecond * initialSpeedMetersPerSecond;
        var kineticEnergyChangeJoules = -chargeCoulombs * potentialDifferenceVolts;
        var finalKineticEnergyJoules =
            initialKineticEnergyJoules + kineticEnergyChangeJoules;

        if (!double.IsFinite(finalKineticEnergyJoules) || finalKineticEnergyJoules < 0)
        {
            error = "The potential difference would require a negative final kinetic energy.";
            return false;
        }

        var finalSpeedSquared = 2 * finalKineticEnergyJoules / massKilograms;
        var finalSpeed = Math.Sqrt(finalSpeedSquared);
        if (!double.IsFinite(finalSpeed))
        {
            error = "The supplied values exceed the supported calculation range.";
            return false;
        }

        result = new ChargedParticleAccelerationResult(
            initialKineticEnergyJoules / ElementaryChargeCoulombs,
            kineticEnergyChangeJoules / ElementaryChargeCoulombs,
            finalKineticEnergyJoules / ElementaryChargeCoulombs,
            finalSpeed);
        return true;
    }
}
