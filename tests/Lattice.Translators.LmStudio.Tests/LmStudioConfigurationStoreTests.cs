using System.Text.Json;
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
            }
        };

        try
        {
            await store.SaveAsync(configuration);

            Assert.True(store.TryLoad(out var loaded, out var error), error);
            Assert.Equal("publisher/gemma-270m-it", loaded.LmStudio.ModelKey);
            Assert.Equal("gemma-local", loaded.LmStudio.ModelIdentifier);
            Assert.Equal("off", loaded.LmStudio.Reasoning);
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

        Assert.True(store.TryLoad(out var loaded, out var error), error);
        Assert.Equal("lms", loaded.LmStudio.LmsExecutable);
        Assert.Equal("off", loaded.LmStudio.Reasoning);
        Assert.Null(loaded.LmStudio.ModelKey);
    }

    [Fact]
    public void Rejects_remote_server_addresses()
    {
        var configuration = new LmStudioConfiguration
        {
            ApiUrl = "http://192.168.1.12:1234/v1/chat/completions"
        };

        Assert.False(configuration.TryValidate(out var error));
        Assert.Contains("loopback", error);
    }

    [Fact]
    public void Rejects_unsupported_reasoning_settings()
    {
        var configuration = new LmStudioConfiguration
        {
            Reasoning = "disabled"
        };

        Assert.False(configuration.TryValidate(out var error));
        Assert.Contains("reasoning", error);
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

            Assert.False(store.TryLoad(out _, out var error));
            Assert.Contains("invalid JSON", error);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
