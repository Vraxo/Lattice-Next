using System.Collections.Immutable;

namespace Lattice.Client;

/// <summary>The status and source references for one stated preference.</summary>
public sealed record LatticeObjectivePreferenceAssessment(
    string Name,
    string Status,
    ImmutableArray<LatticeObjectiveEvidenceSource> Evidence);
