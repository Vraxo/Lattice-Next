using System.Collections.Immutable;

/// <summary>The conditions evaluated for one candidate.</summary>
public sealed record LatticeCandidateAssessment(string Candidate, bool HasHardConstraintViolation, bool HasUnresolvedHardConstraint, ImmutableArray<LatticeObjectiveConditionAssessment> Conditions);
