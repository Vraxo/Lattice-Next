using System.Collections.Immutable;

namespace Lattice.Core.Semantics;

public sealed record ObjectiveTemporalSequenceValue(
    TimeOnly EarliestStart,
    TimeOnly LatestEnd,
    ImmutableArray<ObjectiveTemporalStage> Stages) : ObjectiveConditionValue;
