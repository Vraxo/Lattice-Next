using System.Collections.Immutable;

namespace Lattice.Core.Semantics;

public sealed record Conjunction(ImmutableArray<SemanticFormula> Conjuncts) : SemanticFormula;
