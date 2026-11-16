using System.Collections.Immutable;

namespace Lattice.Core.Generation;

public sealed record GenerationGoal(
    string Effect,
    string ArgumentKind,
    string ArgumentValue,
    ImmutableArray<string> AvailableCapabilities);
