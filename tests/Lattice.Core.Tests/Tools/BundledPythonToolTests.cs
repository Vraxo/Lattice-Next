using Lattice.Core.Tools;
using Lattice.Tooling.Stdio;
using Xunit;

namespace Lattice.Core.Tests.Tools;

public sealed class BundledPythonToolTests
{
    [Theory]
    [InlineData("""{"left":12,"operation":"multiply","right":3}""", "36")]
    [InlineData("""{"left":12.5,"operation":"add","right":3.25}""", "15.75")]
    [InlineData("""{"left":12,"operation":"subtract","right":3}""", "9")]
    [InlineData("""{"left":12,"operation":"divide","right":3}""", "4")]
    public async Task Invokes_the_discovered_python_calculator(
        string arguments,
        string expected)
    {
        var registry = CreateBundledToolRegistry();

        var result = await registry.TryInvokeAsync(
            "calculator",
            arguments,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(expected, result.Observation!.Content);
        Assert.Equal("calculator", result.Observation.ToolId);
    }

    [Theory]
    [InlineData("""{"value":150,"from":"cm","to":"m"}""", "1.5")]
    [InlineData("""{"value":2,"from":"km","to":"m"}""", "2000")]
    [InlineData("""{"value":2500,"from":"mg","to":"g"}""", "2.5")]
    [InlineData("""{"value":2,"from":"h","to":"min"}""", "120")]
    [InlineData("""{"value":1,"from":"l","to":"ml"}""", "1000")]
    public async Task Invokes_the_discovered_python_unit_converter(
        string arguments,
        string expected)
    {
        var registry = CreateBundledToolRegistry();

        var result = await registry.TryInvokeAsync(
            "unit-converter",
            arguments,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(expected, result.Observation!.Content);
        Assert.Equal("unit-converter", result.Observation.ToolId);
    }

    [Theory]
    [InlineData("""{"left":1,"operation":"divide","right":0}""", "Division by zero")]
    [InlineData("""{"left":1,"operation":"power","right":2}""", "Operation must be")]
    public async Task Calculator_returns_sourced_failures_for_invalid_operations(
        string arguments,
        string expectedError)
    {
        var result = await CreateBundledToolRegistry().TryInvokeAsync(
            "calculator",
            arguments,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Contains(expectedError, result.Error);
    }

    [Theory]
    [InlineData("""{"value":1,"from":"m","to":"kg"}""", "incompatible dimensions")]
    [InlineData("""{"value":1,"from":"furlong","to":"m"}""", "Unsupported source unit")]
    [InlineData("""{"value":1,"from":"m","to":"parsec"}""", "Unsupported destination unit")]
    public async Task Unit_converter_rejects_unknown_or_incompatible_units(
        string arguments,
        string expectedError)
    {
        var result = await CreateBundledToolRegistry().TryInvokeAsync(
            "unit-converter",
            arguments,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Contains(expectedError, result.Error);
    }

    [Fact]
    public void Discovers_both_bundled_tools_from_manifests()
    {
        var registry = CreateBundledToolRegistry();

        Assert.Contains(registry.Descriptors, descriptor => descriptor.Id == "calculator");
        Assert.Contains(registry.Descriptors, descriptor => descriptor.Id == "unit-converter");
    }

    private static ToolRegistry CreateBundledToolRegistry()
    {
        var repositoryRoot = FindRepositoryRoot();
        var toolsDirectory = Path.Combine(repositoryRoot, "tools");
        var discovered = StdioToolProvider.TryDiscover(
            toolsDirectory,
            out var tools,
            out var error);

        Assert.True(discovered, error);
        Assert.Contains(tools, tool => tool.Descriptor.Id == "calculator");
        Assert.Contains(tools, tool => tool.Descriptor.Id == "unit-converter");

        return new ToolRegistry(
            tools,
            executionTimeout: TimeSpan.FromSeconds(10));
    }

    private static string FindRepositoryRoot()
    {
        var startingDirectories = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        };

        foreach (var start in startingDirectories)
        {
            DirectoryInfo? directory = new DirectoryInfo(start);

            while (directory is not null)
            {
                var calculatorManifest = Path.Combine(
                    directory.FullName,
                    "tools",
                    "calculator",
                    "tool.json");

                if (File.Exists(calculatorManifest))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate the repository tools/calculator/tool.json manifest.");
    }
}
