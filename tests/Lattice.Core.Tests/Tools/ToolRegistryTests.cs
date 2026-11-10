using Lattice.Core.Tools;
using Xunit;

namespace Lattice.Core.Tests.Tools;

public sealed class ToolRegistryTests
{
    private readonly ToolRegistry registry = new([new RegistryEchoTool()]);

    [Theory]
    [InlineData("""{"value":12}""", "12")]
    [InlineData("""{"value":12.5}""", "12.5")]
    public async Task Invokes_a_registered_tool_and_returns_a_sourced_observation(
        string arguments,
        string expected)
    {
        var result = await registry.TryInvokeAsync(
            "number-echo",
            arguments,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal("number-echo", result.Observation!.ToolId);
        Assert.Equal(expected, result.Observation.Content);
    }

    [Fact]
    public async Task Rejects_unknown_tools()
    {
        var result = await registry.TryInvokeAsync(
            "not-registered",
            "{}",
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Observation);
        Assert.Contains("No tool is registered", result.Error);
    }

    [Theory]
    [InlineData("""{}""", "value")]
    [InlineData("""{"value":12,"extra":true}""", "extra")]
    [InlineData("""{"value":"twelve"}""", "value")]
    [InlineData("""[]""", "JSON object")]
    [InlineData("""{ invalid }""", "valid JSON")]
    [InlineData("""{"value":12,"value":13}""", "more than once")]
    public async Task Rejects_invalid_arguments_before_invoking_the_tool(
        string arguments,
        string expectedError)
    {
        var result = await registry.TryInvokeAsync(
            "number-echo",
            arguments,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Observation);
        Assert.Contains(expectedError, result.Error);
    }

    [Fact]
    public async Task Denies_tools_when_their_required_permissions_are_not_granted()
    {
        var restrictedRegistry = new ToolRegistry([new BlockingWriteTool()]);

        var result = await restrictedRegistry.TryInvokeAsync(
            "blocking-write",
            "{}",
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Contains("permissions", result.Error);
    }

    [Fact]
    public async Task Applies_permission_grants_only_to_the_named_tool()
    {
        var scopedRegistry = new ToolRegistry(
            [
                new RegistryEchoTool("allowed-read", ToolPermission.Read),
                new RegistryEchoTool("denied-read", ToolPermission.Read)
            ],
            toolPermissions: new Dictionary<string, ToolPermission>
            {
                ["ALLOWED-READ"] = ToolPermission.Read
            });

        var allowed = await scopedRegistry.TryInvokeAsync(
            "allowed-read",
            """{"value":7}""",
            TestContext.Current.CancellationToken);
        var denied = await scopedRegistry.TryInvokeAsync(
            "denied-read",
            """{"value":7}""",
            TestContext.Current.CancellationToken);

        Assert.True(allowed.IsSuccess, allowed.Error);
        Assert.Equal("7", allowed.Observation!.Content);
        Assert.False(denied.IsSuccess);
        Assert.Contains("permissions", denied.Error);
    }

    [Fact]
    public async Task Bounds_tool_execution_time_even_when_the_tool_waits_for_cancellation()
    {
        var boundedRegistry = new ToolRegistry(
            [new BlockingWriteTool()],
            ToolPermission.Write,
            TimeSpan.FromMilliseconds(25));

        var result = await boundedRegistry.TryInvokeAsync(
            "blocking-write",
            "{}",
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("Tool execution exceeded its time limit.", result.Error);
    }

    [Fact]
    public void Exposes_tool_descriptors_without_tool_specific_registry_logic()
    {
        var descriptor = Assert.Single(registry.Descriptors);

        Assert.Equal("number-echo", descriptor.Id);
        Assert.Equal("Number echo", descriptor.Name);
        Assert.Single(descriptor.Inputs);
        Assert.Equal("value", descriptor.Inputs[0].Name);
        Assert.Equal(ToolInputType.Number, descriptor.Inputs[0].Type);
    }
}
