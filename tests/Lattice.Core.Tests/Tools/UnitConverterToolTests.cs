using Lattice.Core.Tools;
using Xunit;

namespace Lattice.Core.Tests.Tools;

public sealed class UnitConverterToolTests
{
    private readonly ToolRegistry registry = new([new UnitConverterTool()]);

    [Theory]
    [InlineData("""{"value":150,"from":"cm","to":"m"}""", "1.5")]
    [InlineData("""{"value":2,"from":"km","to":"m"}""", "2000")]
    [InlineData("""{"value":2500,"from":"mg","to":"g"}""", "2.5")]
    [InlineData("""{"value":2,"from":"h","to":"min"}""", "120")]
    [InlineData("""{"value":1,"from":"l","to":"ml"}""", "1000")]
    public async Task Converts_values_between_compatible_units(
        string arguments,
        string expected)
    {
        var result = await registry.TryInvokeAsync(
            "unit-converter",
            arguments,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(expected, result.Observation!.Content);
        Assert.Equal("unit-converter", result.Observation.ToolId);
    }

    [Theory]
    [InlineData("""{"value":1,"from":"m","to":"kg"}""", "incompatible dimensions")]
    [InlineData("""{"value":1,"from":"furlong","to":"m"}""", "Unsupported source unit")]
    [InlineData("""{"value":1,"from":"m","to":"parsec"}""", "Unsupported destination unit")]
    public async Task Rejects_unknown_or_incompatible_units(
        string arguments,
        string expectedError)
    {
        var result = await registry.TryInvokeAsync(
            "unit-converter",
            arguments,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Contains(expectedError, result.Error);
    }

    [Fact]
    public void Exposes_a_generic_descriptor()
    {
        var descriptor = Assert.Single(registry.Descriptors);

        Assert.Equal("unit-converter", descriptor.Id);
        Assert.Equal(3, descriptor.Inputs.Length);
        Assert.Equal("value", descriptor.Inputs[0].Name);
        Assert.Equal(ToolInputType.Number, descriptor.Inputs[0].Type);
    }
}
