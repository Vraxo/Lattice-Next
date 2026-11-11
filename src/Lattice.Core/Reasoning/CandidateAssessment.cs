using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

public sealed record CandidateAssessment(
    SemanticIdentifier Candidate,
    ImmutableArray<ObjectiveConditionAssessment> Conditions)
{
    public bool HasHardConstraintViolation => Conditions.Any(condition =>
        condition.Role == ObjectiveConditionRole.HardConstraint &&
        condition.Status == ObjectiveConditionAssessmentStatus.Violated);

    public bool HasUnresolvedHardConstraint => Conditions.Any(condition =>
        condition.Role == ObjectiveConditionRole.HardConstraint &&
        condition.Status is ObjectiveConditionAssessmentStatus.Unknown or
            ObjectiveConditionAssessmentStatus.Conflicting);
}
