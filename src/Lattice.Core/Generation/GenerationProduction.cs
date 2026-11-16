using System.Collections.Immutable;

namespace Lattice.Core.Generation;

public sealed record GenerationProduction(
    string Head,
    ImmutableArray<GenerationSymbol> Symbols);
