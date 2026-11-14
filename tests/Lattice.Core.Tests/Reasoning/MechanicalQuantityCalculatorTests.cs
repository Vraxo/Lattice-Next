using Lattice.Core.Reasoning;
using Xunit;

namespace Lattice.Core.Tests.Reasoning;

public sealed class MechanicalQuantityCalculatorTests
{
    [Fact]
    public void Converts_supported_measurement_units_to_si_base_units()
    {
        Assert.True(SemanticUnit.TryConvertToBase(2m, "mm", out var dimension, out var value));
        Assert.Equal("length", dimension);
        Assert.Equal(0.002m, value);
        Assert.Equal("m", SemanticUnit.GetBaseUnit(dimension));
        Assert.True(SemanticUnit.TryConvertToBase(3m, "GPa", out var pressureDimension, out var pressure));
        Assert.Equal("pressure", pressureDimension);
        Assert.Equal(3000000000m, pressure);
    }

    [Fact]
    public void Calculates_axial_stress_strain_and_stiffness()
    {
        var success = MechanicalQuantityCalculator.TryCalculateAxialResponse(
            100m, 0.0001m, 200000000000m, 2m, out var result, out var error);
        Assert.True(success, error);
        Assert.NotNull(result);
        Assert.Equal(1000000m, result.StressPascals);
        Assert.Equal(0.000005m, result.Strain);
        Assert.Equal(10000000m, result.StiffnessNewtonsPerMeter);
    }

    [Fact]
    public void Rejects_non_physical_dimensions_and_negative_force()
    {
        Assert.False(MechanicalQuantityCalculator.TryCalculateAxialResponse(-1m, 0.0001m, 200000000000m, 2m, out _, out _));
        Assert.False(MechanicalQuantityCalculator.TryCalculateAxialResponse(100m, 0m, 200000000000m, 2m, out _, out _));
    }
}
