using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lattice.Core.Tools;

namespace Lattice.Tooling.Stdio;

public static class StdioToolProvider
{
    private const string ManifestFileName = "tool.json";
    
    public static bool TryDiscover(
        string toolsDirectory,
        out IReadOnlyList<ITool> tools,
        out string? error)
    {
        tools = Array.Empty<ITool>();
        error = null;

        if (string.IsNullOrWhiteSpace(toolsDirectory))
        {
            error = "The tools directory must be specified.";
            return false;
        }

        try
        {
            var rootDirectory = Path.GetFullPath(toolsDirectory);

            if (!Directory.Exists(rootDirectory))
            {
                return true;
            }

            var discovered = new List<ITool>();
            var identifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var toolDirectory in Directory
                .EnumerateDirectories(rootDirectory)
                .OrderBy(path => path, StringComparer.Ordinal))
            {
                var manifestPath = Path.Combine(toolDirectory, ManifestFileName);

                if (!File.Exists(manifestPath))
                {
                    continue;
                }

                if (!StdioToolManifestParser.TryLoadTool(manifestPath, toolDirectory, out var tool, out error))
                {
                    return false;
                }

                if (!identifiers.Add(tool!.Descriptor.Id))
                {
                    error = $"More than one external tool uses ID '{tool.Descriptor.Id}'.";
                    return false;
                }

                discovered.Add(tool);
            }

            tools = discovered;
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            error = "The configured tools directory could not be read.";
            return false;
        }
    }











;












}
