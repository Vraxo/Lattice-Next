using System.Collections.Immutable;

namespace Lattice.Core.Generation;

/// <summary>
/// The language names this system declares. They are read from the embedded generation packs, so a name exists
/// because a pack spells that language, and shared code can recognise one without naming it.
/// </summary>
public static class GenerationLanguages
{
    private static readonly Lazy<ImmutableArray<string>> Declared = new(LoadDeclared);

    /// <summary>Every declared language name, ordered so a request's requirements are built the same way twice.</summary>
    public static ImmutableArray<string> Names => Declared.Value;

    /// <summary>Whether a pack spells this language.</summary>
    public static bool IsDeclared(string? language) =>
        language is not null && Names.Contains(language, StringComparer.OrdinalIgnoreCase);

    private static ImmutableArray<string> LoadDeclared()
    {
        var loader = new GenerationLanguagePackLoader();
        return loader.TryLoadEmbedded(out var packs, out _)
            ? packs.Keys.OrderBy(name => name, StringComparer.Ordinal).ToImmutableArray()
            : ImmutableArray<string>.Empty;
    }
}
