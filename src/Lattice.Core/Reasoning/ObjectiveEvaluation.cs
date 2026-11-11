using System.Collections.Immutable;

namespace Lattice.Core.Reasoning;

public sealed record ObjectiveEvaluation(ImmutableArray<CandidateAssessment> Candidates);
