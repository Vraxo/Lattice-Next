using System.Text.Json;
using Lattice.Core.Intelligence;
using Lattice.Core.Tools;
using Lattice.Core.Tests.Tools;
using Xunit;

namespace Lattice.Core.Tests.Tools;

public sealed class WebSearchToolTests
{
    [Fact]
    public void Discovers_web_search_through_its_manifest_and_requires_read_permission()
    {
        var registry = BundledToolTestSupport.CreateRegistry();
        var descriptor = Assert.Single(
            registry.Descriptors,
            item => item.Id == "web-search");

        Assert.Equal(ToolPermission.Read, descriptor.RequiredPermissions);

        var recognizers = descriptor.Intents.Select(
            definition => new ManifestToolIntentRecognizer(descriptor, definition));
        var resolution = new ToolIntentResolver(recognizers).Resolve(
            "Please search the web for the latest Lattice architecture notes.");

        Assert.True(resolution.IsResolved);
        Assert.Equal("web-search", resolution.Intent!.ToolId);

        using var arguments = JsonDocument.Parse(resolution.Intent.ArgumentsJson);
        Assert.Equal(
            "the latest Lattice architecture notes",
            arguments.RootElement.GetProperty("query").GetString());
    }

    [Fact]
    public async Task Registry_denies_web_search_until_read_permission_is_granted()
    {
        var registry = BundledToolTestSupport.CreateRegistry();
        var denied = await registry.TryInvokeAsync(
            "web-search",
            """{"query":"lattice"}""",
            TestContext.Current.CancellationToken);

        Assert.False(denied.IsSuccess);
        Assert.Contains("permissions", denied.Error);
    }
}
