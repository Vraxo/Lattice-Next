using System.Collections.Immutable;

namespace Lattice.Client;

/// <summary>Candidate assessments for the active session objective.</summary>
public sealed record LatticeObjectiveEvaluation(ImmutableArray<LatticeCandidateAssessment> Candidates)
{
    public ImmutableArray<LatticeObjectivePreferenceTradeOff> PreferenceTradeOffs { get; init; } = [];

    public LatticeObjectiveRecommendation Recommendation { get; init; } = new(
        "NoEligibleCandidates",
        [],
        [],
        HasIncompletePreferenceEvidence: true);

    public ImmutableArray<LatticeObjectiveEvidenceRequest> EvidenceRequests { get; init; } = [];
}
