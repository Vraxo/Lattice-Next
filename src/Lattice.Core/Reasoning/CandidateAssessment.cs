using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

public sealed record CandidateAssessment(
    SemanticIdentifier Candidate,
    ImmutableArray<ObjectiveConditionAssessment> Conditions)
{
    public ImmutableArray<KnowledgeAttribute> Highlights { get; init; } = [];

    public ImmutableArray<string> MissingHardConstraintAttributes { get; init; } = [];

    public bool HasHardConstraintViolation => Conditions.Any(condition =>
        condition.Role == ObjectiveConditionRole.HardConstraint &&
        condition.Status == ObjectiveConditionAssessmentStatus.Violated);

    public bool HasUnresolvedHardConstraint => Conditions.Any(condition =>
        condition.Role == ObjectiveConditionRole.HardConstraint &&
        condition.Status is ObjectiveConditionAssessmentStatus.Unknown or
            ObjectiveConditionAssessmentStatus.Conflicting);

    public CandidateFeasibility Feasibility
    {
        get
        {
            var hasApplicableHardConstraint = Conditions.Any(condition =>
                condition.Role == ObjectiveConditionRole.HardConstraint &&
                condition.Status != ObjectiveConditionAssessmentStatus.NotApplicable);

            if (!hasApplicableHardConstraint)
            {
                return CandidateFeasibility.NotEvaluated;
            }

            if (HasHardConstraintViolation)
            {
                return CandidateFeasibility.Excluded;
            }

            return HasUnresolvedHardConstraint
                ? CandidateFeasibility.Unresolved
                : CandidateFeasibility.Eligible;
        }
    }
}
