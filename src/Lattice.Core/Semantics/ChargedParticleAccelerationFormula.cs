namespace Lattice.Core.Semantics;

public sealed record ChargedParticleAccelerationFormula(
    string Particle,
    decimal InitialSpeedMetersPerSecond,
    decimal PotentialDifferenceVolts) : SemanticFormula;
