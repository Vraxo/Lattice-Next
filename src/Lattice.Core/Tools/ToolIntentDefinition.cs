using System.Collections.Immutable;

namespace Lattice.Core.Tools;

public sealed record ToolIntentDefinition(
    string Pattern,
    ImmutableDictionary<string, ToolIntentBinding> Arguments,
    string ResponseTemplate);
