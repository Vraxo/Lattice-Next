using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

public sealed record ObjectiveEvaluation(ImmutableArray<CandidateAssessment> Candidates)
{
    public ImmutableArray<ObjectivePreferenceTradeOff> PreferenceTradeOffs { get; init; } = [];
}
