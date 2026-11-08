using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

public sealed record KnowledgeFact(
    PredicateApplication Predicate,
    string SourceText);
