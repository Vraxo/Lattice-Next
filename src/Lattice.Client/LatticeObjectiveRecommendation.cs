using System.Collections.Immutable;

namespace Lattice.Client;

/// <summary>A non-scored shortlist derived from hard-constraint feasibility and stated preferences.</summary>
public sealed record LatticeObjectiveRecommendation(
    string Status,
    ImmutableArray<string> ShortlistedCandidates,
    ImmutableArray<string> DominatedCandidates,
    bool HasIncompletePreferenceEvidence);
