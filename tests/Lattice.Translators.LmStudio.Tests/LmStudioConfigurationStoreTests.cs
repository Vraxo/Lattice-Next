using System.Text.Json;
using AwesomeAssertions;
using Lattice.Translators.LmStudio;
using Xunit;

namespace Lattice.Translators.LmStudio.Tests;

public sealed class LmStudioConfigurationStoreTests
{
    [Fact]
    public async Task Saves_and_loads_a_local_configuration()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"lattice-{Guid.NewGuid():N}.json");
        var store = new LmStudioConfigurationStore(path);
        var configuration = new LatticeConfiguration
        {
            LmStudio = new LmStudioConfiguration
            {
                ModelKey = "publisher/gemma-270m-it",
                ModelIdentifier = "gemma-local",
                Reasoning = "off"
            },
            ToolPermissions = new Dictionary<string, ToolPermission>
            {
                ["web-search"] = ToolPermission.Read,
                ["trusted-sync"] = ToolPermission.Read | ToolPermission.Write
            }
        };

        try
        {
            await store.SaveAsync(configuration);

            store.TryLoad(out var loaded, out var error).Should().BeTrue(error);
            loaded.LmStudio.ModelKey.Should().Be("publisher/gemma-270m-it");
            loaded.LmStudio.ModelIdentifier.Should().Be("gemma-local");
            loaded.LmStudio.Reasoning.Should().Be("off");
            loaded.ToolPermissions.Should().ContainKey("web-search")
                .WhoseValue.Should().Be(ToolPermission.Read);
            loaded.ToolPermissions.Should().ContainKey("trusted-sync")
                .WhoseValue.Should().Be(ToolPermission.Read | ToolPermission.Write);
            File.ReadAllText(path).Should().Contain("\"web-search\": \"read\"");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Uses_defaults_when_the_configuration_file_does_not_exist()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"lattice-{Guid.NewGuid():N}.json");
        var store = new LmStudioConfigurationStore(path);

        store.TryLoad(out var loaded, out var error).Should().BeTrue(error);
        loaded.LmStudio.LmsExecutable.Should().Be("lms");
        loaded.LmStudio.Reasoning.Should().Be("off");
        loaded.LmStudio.ModelKey.Should().BeNull();
        loaded.ToolPermissions.Should().BeEmpty();
    }

    [Fact]
    public void Rejects_unknown_or_empty_persisted_tool_permissions()
    {
        var configuration = new LatticeConfiguration
        {
            ToolPermissions = new Dictionary<string, ToolPermission>
            {
                ["web-search"] = (ToolPermission)8
            }
        };

        configuration.TryValidate(out var error).Should().BeFalse();
        error.Should().Contain("known, non-empty permissions");
    }

    [Fact]
    public void Rejects_remote_server_addresses()
    {
        var configuration = new LmStudioConfiguration
        {
            ApiUrl = "http://192.168.1.12:1234/v1/chat/completions"
        };

        configuration.TryValidate(out var error).Should().BeFalse();
        error.Should().Contain("loopback");
    }

    [Fact]
    public void Rejects_unsupported_reasoning_settings()
    {
        var configuration = new LmStudioConfiguration
        {
            Reasoning = "disabled"
        };

        configuration.TryValidate(out var error).Should().BeFalse();
        error.Should().Contain("reasoning");
    }

    [Fact]
    public void Rejects_invalid_configuration_files()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"lattice-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{ invalid");

        try
        {
            var store = new LmStudioConfigurationStore(path);

            store.TryLoad(out _, out var error).Should().BeFalse();
            error.Should().Contain("invalid JSON");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
