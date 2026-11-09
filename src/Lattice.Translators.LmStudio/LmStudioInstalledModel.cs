using System.Collections.Immutable;

namespace Lattice.Translators.LmStudio;

public sealed record LmStudioInstalledModel(
    string Key,
    string DisplayName,
    string? Parameters,
    long SizeBytes,
    ImmutableArray<string> LoadedInstanceIds);
