using System.Collections.Immutable;

namespace Lattice.Core.Semantics;

public sealed record ObjectiveTextSetValue(ImmutableArray<string> Values) : ObjectiveConditionValue;
