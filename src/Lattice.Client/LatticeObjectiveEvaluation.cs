using System.Collections.Immutable;

namespace Lattice.Client;

/// <summary>Candidate assessments for the active session objective.</summary>
public sealed record LatticeObjectiveEvaluation(ImmutableArray<LatticeCandidateAssessment> Candidates);
