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
}
