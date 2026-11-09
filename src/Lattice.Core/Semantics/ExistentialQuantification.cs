namespace Lattice.Core.Semantics;

public sealed record ExistentialQuantification(
    VariableTerm Variable,
    SemanticFormula Body) : SemanticFormula;
