using Lattice.Core.Intelligence;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

namespace Lattice.Runtime;

internal static class SemanticWorldModelResourceLoader
{
    public static SemanticWorldModel Load()
    {
        var semanticJson = LoadEmbeddedSemanticWorldModel();
        if (!new SemanticWorldModelLoader().TryLoad(
            semanticJson,
            out var loadedSemanticWorldModel,
            out var error))
        {
            throw new InvalidOperationException(
                error ?? "The embedded semantic world model could not be loaded.");
        }

        return loadedSemanticWorldModel!;
    }
    private static string LoadEmbeddedSemanticWorldModel()
    {
        const string resourcePrefix = "Lattice.Runtime.SemanticWorldModel.";
        var assembly = typeof(SemanticWorldModelResourceLoader).Assembly;
        var resources = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(resourcePrefix, StringComparison.Ordinal) &&
                name.EndsWith(".json", StringComparison.Ordinal))
            .OrderBy(name => name.EndsWith(".core.json", StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(name => name, StringComparer.Ordinal)
            .ToArray();

        if (resources.Length == 0)
        {
            throw new InvalidOperationException("No embedded semantic world model modules were found.");
        }

        System.Text.Json.Nodes.JsonObject? combined = null;
        var collectionNames = new[]
        {
            "concepts", "lexemes", "relations", "definitions",
            "relationAssertions", "dataProperties", "dataAssertions"
        };

        foreach (var resource in resources)
        {
            using var stream = assembly.GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException($"The semantic world model module '{resource}' is missing.");
            using var reader = new StreamReader(stream);
            var module = System.Text.Json.Nodes.JsonNode.Parse(reader.ReadToEnd()) as System.Text.Json.Nodes.JsonObject
                ?? throw new InvalidOperationException($"The semantic world model module '{resource}' is invalid.");

            if (combined is null)
            {
                combined = (System.Text.Json.Nodes.JsonObject)module.DeepClone();
                continue;
            }

            foreach (var collectionName in collectionNames)
            {
                if (module[collectionName] is not System.Text.Json.Nodes.JsonArray source ||
                    combined[collectionName] is not System.Text.Json.Nodes.JsonArray target)
                {
                    throw new InvalidOperationException(
                        $"The semantic world model module '{resource}' is missing '{collectionName}'.");
                }

                foreach (var item in source)
                {
                    target.Add(item?.DeepClone());
                }
            }
        }

        return combined?.ToJsonString()
            ?? throw new InvalidOperationException("The embedded semantic world model is empty.");
    }
}
