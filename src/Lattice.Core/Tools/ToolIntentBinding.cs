using System.Text.Json;

namespace Lattice.Core.Tools;

public sealed record ToolIntentBinding(
    string? Reference,
    JsonElement? Constant);
