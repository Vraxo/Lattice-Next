using System.Collections.Immutable;

namespace Lattice.Core.Tools;

public sealed record ToolDescriptor(
    string Id,
    string Name,
    string Description,
    ImmutableArray<ToolInputDescriptor> Inputs,
    ToolPermission RequiredPermissions = ToolPermission.None,
    ImmutableArray<ToolIntentDefinition> Intents = default);
