using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

public sealed record ObjectiveEvaluation(ImmutableArray<CandidateAssessment> Candidates)
{
    public ImmutableArray<ObjectivePreferenceTradeOff> PreferenceTradeOffs { get; init; } = [];

    public ImmutableArray<ObjectiveEvidenceRequest> EvidenceRequests { get; init; } = [];

    public ObjectiveRecommendation Recommendation { get; init; } = new(
        ObjectiveRecommendationStatus.NoEligibleCandidates,
        [],
        [],
        HasIncompletePreferenceEvidence: true);
}
