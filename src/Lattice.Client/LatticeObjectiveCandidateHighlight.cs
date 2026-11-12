using System.Collections.Immutable;

namespace Lattice.Client;

/// <summary>A knowledge-pack-selected candidate detail and its source references.</summary>
public sealed record LatticeObjectiveCandidateHighlight(
    string Name,
    ImmutableArray<LatticeObjectiveEvidenceSource> Evidence);
