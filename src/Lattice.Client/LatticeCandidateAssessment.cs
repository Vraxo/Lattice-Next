using System.Collections.Immutable;

namespace Lattice.Client;

/// <summary>The conditions evaluated for one candidate.</summary>
public sealed record LatticeCandidateAssessment(
    string Candidate,
    bool HasHardConstraintViolation,
    bool HasUnresolvedHardConstraint,
    ImmutableArray<LatticeObjectiveConditionAssessment> Conditions)
{
    /// <summary>Whether the candidate satisfies all applicable hard constraints.</summary>
    public string Feasibility { get; init; } = "NotEvaluated";
}
