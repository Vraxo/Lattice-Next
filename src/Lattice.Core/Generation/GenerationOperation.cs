using System.Collections.Immutable;

namespace Lattice.Core.Generation;

public sealed record GenerationOperation(
    string Id,
    string Symbol,
    string Effect,
    string ArgumentKind,
    ImmutableArray<string> Prerequisites);
