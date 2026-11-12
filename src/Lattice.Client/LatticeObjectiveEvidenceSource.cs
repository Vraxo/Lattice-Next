namespace Lattice.Client;

/// <summary>A source supporting a candidate attribute used during objective evaluation.</summary>
public sealed record LatticeObjectiveEvidenceSource(
    string Kind,
    string SourceText,
    string? SourceId,
    DateTimeOffset? RetrievedAt = null,
    DateTimeOffset? FreshUntil = null);
