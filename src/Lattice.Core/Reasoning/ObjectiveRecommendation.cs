using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

public enum ObjectiveRecommendationStatus
{
    NoEligibleCandidates,
    OnlyEligibleCandidate,
    UniquePreferenceLeader,
    MultipleNonDominatedCandidates
}

/// <summary>
/// A conservative preference-based shortlist. It never assigns weights or treats unknown
/// preference evidence as equivalent to a satisfied preference.
/// </summary>
public sealed record ObjectiveRecommendation(
    ObjectiveRecommendationStatus Status,
    ImmutableArray<SemanticIdentifier> ShortlistedCandidates,
    ImmutableArray<SemanticIdentifier> DominatedCandidates,
    bool HasIncompletePreferenceEvidence);
