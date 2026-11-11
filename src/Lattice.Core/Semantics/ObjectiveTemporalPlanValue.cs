using System.Collections.Immutable;

namespace Lattice.Core.Semantics;

/// <summary>
/// An ordered candidate-provided template for checking whether a sequence fits a time window.
/// </summary>
public sealed record ObjectiveTemporalPlanValue(
    ImmutableArray<ObjectiveTemporalStage> Stages) : ObjectiveConditionValue;
