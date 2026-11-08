using System.Collections.Immutable;

namespace Lattice.Core.Semantics;

public sealed record PredicateApplication(
    SemanticIdentifier Predicate,
    ImmutableArray<SemanticTerm> Arguments) : SemanticFormula;
