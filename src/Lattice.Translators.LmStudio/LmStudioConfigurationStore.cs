using System.Text.Json;

namespace Lattice.Translators.LmStudio;

public sealed class LmStudioConfigurationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public LmStudioConfigurationStore(string? path = null)
    {
        Path = System.IO.Path.GetFullPath(
            path ?? System.IO.Path.Combine(Environment.CurrentDirectory, "lattice.config.json"));
    }

    public string Path { get; }

    public bool TryLoad(
        out LatticeConfiguration configuration,
        out string? error)
    {
        configuration = new LatticeConfiguration();
        error = null;

        if (!File.Exists(Path))
        {
            return true;
        }

        try
        {
            var json = File.ReadAllText(Path);
            var loaded = JsonSerializer.Deserialize<LatticeConfiguration>(json, JsonOptions);

            if (loaded is null || loaded.LmStudio is null)
            {
                error = "The lattice.config.json file does not contain a valid configuration.";
                return false;
            }

            if (!loaded.LmStudio.TryValidate(out error))
            {
                return false;
            }

            configuration = loaded;
            return true;
        }
        catch (JsonException exception)
        {
            error = $"The lattice.config.json file contains invalid JSON: {exception.Message}";
            return false;
        }
        catch (IOException exception)
        {
            error = $"The lattice.config.json file could not be read: {exception.Message}";
            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            error = $"The lattice.config.json file could not be read: {exception.Message}";
            return false;
        }
    }

    public async Task SaveAsync(
        LatticeConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var settings = configuration.LmStudio;

        if (settings is null)
        {
            throw new InvalidOperationException("The Lattice configuration is invalid.");
        }

        if (!settings.TryValidate(out var error))
        {
            throw new InvalidOperationException(error ?? "The Lattice configuration is invalid.");
        }

        var directory = System.IO.Path.GetDirectoryName(Path)
            ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);

        var temporaryPath = $"{Path}.{Guid.NewGuid():N}.tmp";

        try
        {
            var json = JsonSerializer.Serialize(configuration, JsonOptions);
            await File.WriteAllTextAsync(temporaryPath, json, cancellationToken)
                .ConfigureAwait(false);
            File.Move(temporaryPath, Path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
