namespace Lattice.Core.Semantics;

public sealed record ObjectiveTimeWindowValue(TimeOnly Start, TimeOnly End) : ObjectiveConditionValue;
