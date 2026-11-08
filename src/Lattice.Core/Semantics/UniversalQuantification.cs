namespace Lattice.Core.Semantics;

public sealed record UniversalQuantification(
    VariableTerm Variable,
    SemanticFormula Body) : SemanticFormula;
