using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

public sealed record ObjectiveConditionAssessment(
    string ConditionName,
    ObjectiveConditionRole Role,
    ObjectiveConditionAssessmentStatus Status,
    ImmutableArray<KnowledgeAttribute> Evidence)
{
    public ImmutableArray<ObjectiveTemporalStageResult> TemporalWitness { get; init; } = [];

    public ImmutableArray<ObjectivePreferenceAssessment> Preferences { get; init; } = [];
}
