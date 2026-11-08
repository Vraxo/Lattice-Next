using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

public sealed record UniversalRule(
    PredicateApplication Premise,
    PredicateApplication Conclusion,
    string SourceText);
