namespace Lattice.Core.Semantics;

/// <summary>
/// Requests the objects connected to a known subject by a relation expressed as a verb.
/// </summary>
public sealed record SemanticRelationQueryFormula(
    string Subject,
    string Verb) : SemanticFormula;
