using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

public sealed record ProofNode(
    PredicateApplication Predicate,
    string SourceText,
    ProofNode? Premise,
    SemanticIdentifier? ResolvedReferent = null);
