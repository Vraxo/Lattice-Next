using Lattice.Core.Tools;
using Xunit;

namespace Lattice.Core.Tests.Tools;

public sealed class ToolRegistryTests
{
    private readonly ToolRegistry registry = new([new CalculatorTool()]);

    [Theory]
    [InlineData("""{"left":12,"operation":"add","right":3}""", "15")]
    [InlineData("""{"left":12,"operation":"subtract","right":3}""", "9")]
    [InlineData("""{"left":12,"operation":"multiply","right":3}""", "36")]
    [InlineData("""{"left":12,"operation":"divide","right":3}""", "4")]
    public async Task Invokes_calculator_and_returns_a_sourced_observation(
        string arguments,
        string expected)
    {
        var result = await registry.TryInvokeAsync("calculator", arguments);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal("calculator", result.Observation!.ToolId);
        Assert.Equal(expected, result.Observation.Content);
    }

    [Fact]
    public async Task Rejects_unknown_tools()
    {
        var result = await registry.TryInvokeAsync("not-registered", "{}");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Observation);
        Assert.Contains("No tool is registered", result.Error);
    }

    [Theory]
    [InlineData("""{"left":12,"operation":"add"}""", "right")]
    [InlineData("""{"left":12,"operation":"add","right":3,"extra":true}""", "extra")]
    [InlineData("""{"left":"twelve","operation":"add","right":3}""", "left")]
    [InlineData("""[]""", "JSON object")]
    [InlineData("""{ invalid }""", "valid JSON")]
    [InlineData("""{"left":12,"operation":"add","right":3,"right":4}""", "more than once")]
    public async Task Rejects_invalid_arguments_before_invoking_the_tool(
        string arguments,
        string expectedError)
    {
        var result = await registry.TryInvokeAsync("calculator", arguments);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Observation);
        Assert.Contains(expectedError, result.Error);
    }

    [Fact]
    public async Task Reports_division_by_zero_as_a_tool_failure()
    {
        var result = await registry.TryInvokeAsync(
            "calculator",
            """{"left":12,"operation":"divide","right":0}""");

        Assert.False(result.IsSuccess);
        Assert.Equal("Division by zero is undefined.", result.Error);
    }

    [Fact]
    public async Task Reports_unsupported_operations_without_throwing()
    {
        var result = await registry.TryInvokeAsync(
            "calculator",
            """{"left":12,"operation":"power","right":2}""");

        Assert.False(result.IsSuccess);
        Assert.Contains("Operation must be", result.Error);
    }

    [Fact]
    public async Task Denies_tools_when_their_required_permissions_are_not_granted()
    {
        var restrictedRegistry = new ToolRegistry([new BlockingWriteTool()]);

        var result = await restrictedRegistry.TryInvokeAsync("blocking-write", "{}");

        Assert.False(result.IsSuccess);
        Assert.Contains("permissions", result.Error);
    }

    [Fact]
    public async Task Bounds_tool_execution_time_even_when_the_tool_waits_for_cancellation()
    {
        var boundedRegistry = new ToolRegistry(
            [new BlockingWriteTool()],
            ToolPermission.Write,
            TimeSpan.FromMilliseconds(25));

        var result = await boundedRegistry.TryInvokeAsync("blocking-write", "{}");

        Assert.False(result.IsSuccess);
        Assert.Equal("Tool execution exceeded its time limit.", result.Error);
    }

    [Fact]
    public void Exposes_tool_descriptors_without_tool_specific_registry_logic()
    {
        var descriptor = Assert.Single(registry.Descriptors);

        Assert.Equal("calculator", descriptor.Id);
        Assert.Equal("Calculator", descriptor.Name);
        Assert.Equal(3, descriptor.Inputs.Length);
        Assert.Equal("left", descriptor.Inputs[0].Name);
        Assert.Equal(ToolInputType.Number, descriptor.Inputs[0].Type);
    }
}
