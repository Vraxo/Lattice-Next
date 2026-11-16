namespace Lattice.Core.Reasoning;

public sealed record ChargedParticleAccelerationResult(
    double InitialKineticEnergyElectronVolts,
    double KineticEnergyChangeElectronVolts,
    double FinalKineticEnergyElectronVolts,
    double FinalSpeedMetersPerSecond);
