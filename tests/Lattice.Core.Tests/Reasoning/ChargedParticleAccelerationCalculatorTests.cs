using Lattice.Core.Reasoning;
using Xunit;

namespace Lattice.Core.Tests.Reasoning;

public sealed class ChargedParticleAccelerationCalculatorTests
{
    [Fact]
    public void Negative_potential_difference_accelerates_a_positive_proton()
    {
        var success = ChargedParticleAccelerationCalculator.TryCalculateFinalSpeed(
            25000,
            -250,
            1,
            1.007276466621,
            out var result,
            out var error);

        Assert.True(success, error);
        Assert.NotNull(result);
        Assert.InRange(result.FinalSpeedMetersPerSecond, 220000, 221000);
        Assert.InRange(result.InitialKineticEnergyElectronVolts, 3.25, 3.28);
        Assert.Equal(250, result.KineticEnergyChangeElectronVolts, 8);
        Assert.InRange(result.FinalKineticEnergyElectronVolts, 253.25, 253.28);
    }

    [Fact]
    public void Rejects_a_potential_difference_that_would_require_negative_kinetic_energy()
    {
        var success = ChargedParticleAccelerationCalculator.TryCalculateFinalSpeed(
            25000,
            1000,
            1,
            1.007276466621,
            out var result,
            out var error);

        Assert.False(success);
        Assert.Null(result);
        Assert.Contains("negative final kinetic energy", error);
    }
    [Fact]
    public void Neutral_particle_keeps_its_speed_across_a_potential_difference()
    {
        var success = ChargedParticleAccelerationCalculator.TryCalculateFinalSpeed(
            25000,
            -250,
            0,
            1,
            out var result,
            out var error);

        Assert.True(success, error);
        Assert.NotNull(result);
        Assert.Equal(25000, result.FinalSpeedMetersPerSecond, 8);
        Assert.Equal(0, result.KineticEnergyChangeElectronVolts);
    }

}
