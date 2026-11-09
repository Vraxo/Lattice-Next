using Lattice.Core.Tools;
using Lattice.Tooling.Stdio;

namespace Lattice.Core.Tests.Tools;

internal static class BundledToolTestSupport
{
    public static ToolRegistry CreateRegistry()
    {
        var repositoryRoot = FindRepositoryRoot();
        var toolsDirectory = Path.Combine(repositoryRoot, "tools");
        var discovered = StdioToolProvider.TryDiscover(
            toolsDirectory,
            out var tools,
            out var error);

        if (!discovered)
        {
            throw new InvalidOperationException(error ?? "Bundled tools could not be discovered.");
        }

        if (!tools.Any(tool => tool.Descriptor.Id == "calculator") ||
            !tools.Any(tool => tool.Descriptor.Id == "unit-converter"))
        {
            throw new InvalidOperationException(
                "The bundled calculator and unit converter manifests were not both discovered.");
        }

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
