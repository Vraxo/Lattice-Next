namespace Lattice.Core.Semantics;

public sealed record SemanticUtterance(
    UtteranceKind Kind,
    SemanticFormula Meaning,
    string SourceText);
