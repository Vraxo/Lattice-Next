using System.Collections.Immutable;

/// <summary>Candidate assessments for the active session objective.</summary>
public sealed record LatticeObjectiveEvaluation(ImmutableArray<LatticeCandidateAssessment> Candidates);
