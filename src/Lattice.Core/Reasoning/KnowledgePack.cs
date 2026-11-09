namespace Lattice.Core.Reasoning;

public sealed record KnowledgePack(
    int FormatVersion,
    string Id,
    string Name,
    int ClaimCount);
