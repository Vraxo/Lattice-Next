namespace Lattice.Core.Semantics;

public sealed record Implication(
    SemanticFormula Premise,
    SemanticFormula Conclusion) : SemanticFormula;
