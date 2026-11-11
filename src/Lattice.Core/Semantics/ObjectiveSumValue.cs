using System.Collections.Immutable;

namespace Lattice.Core.Semantics;

/// <summary>
/// A declarative sum of other typed attributes belonging to the same candidate.
/// </summary>
public sealed record ObjectiveSumValue(
    ImmutableArray<string> Attributes) : ObjectiveConditionValue;
