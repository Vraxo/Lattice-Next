namespace Lattice.Core.Tools;

public sealed record ToolInputDescriptor(
    string Name,
    string Description,
    ToolInputType Type,
    bool Required = true);
