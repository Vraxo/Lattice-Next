namespace Lattice.Core.Intelligence;

internal sealed record MatchedToolIntent(
    Lattice.Core.Tools.ToolIntentDefinition Definition,
    IReadOnlyDictionary<string, object?> Slots);
