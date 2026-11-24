using System.Collections.Immutable;

namespace Lattice.Core.Generation;

internal static class GenerationLanguagePackSelector
{
    private static readonly Lazy<ImmutableDictionary<string, GenerationLanguagePack>?> EmbeddedPacks =
        new(LoadEmbeddedPacks);

    public static GenerationLanguagePack? Select(ImmutableArray<SequenceSemanticFact> facts)
    {
        var languageFacts = facts
            .Where(fact => fact.Id.StartsWith("language.", StringComparison.Ordinal))
            .ToArray();
        if (languageFacts.Length != 1 || EmbeddedPacks.Value is not { } packs)
        {
            return null;
        }

        var language = languageFacts[0].Id["language.".Length..];
        return packs.TryGetValue(language, out var pack) ? pack : null;
    }

    private static ImmutableDictionary<string, GenerationLanguagePack>? LoadEmbeddedPacks()
    {
        var loader = new GenerationLanguagePackLoader();
        return loader.TryLoadEmbedded(out var packs, out _) ? packs : null;
    }
}
