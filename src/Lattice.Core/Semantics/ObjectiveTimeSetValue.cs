using System.Collections.Immutable;

namespace Lattice.Core.Semantics;

public sealed record ObjectiveTimeSetValue(ImmutableArray<TimeOnly> Times) : ObjectiveConditionValue;
