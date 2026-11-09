using System.Collections.Immutable;

namespace Lattice.Core.Tools;

public sealed record ToolIntentPatternPart(
    ImmutableArray<string> LiteralTokens,
    string? SlotName = null,
    string? SlotType = null,
    bool IsOptional = false);
