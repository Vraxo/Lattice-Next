using System.Collections.Immutable;

namespace Lattice.Client;

/// <summary>The conditions evaluated for one candidate.</summary>
public sealed record LatticeCandidateAssessment(string Candidate, bool HasHardConstraintViolation, bool HasUnresolvedHardConstraint, ImmutableArray<LatticeObjectiveConditionAssessment> Conditions);
