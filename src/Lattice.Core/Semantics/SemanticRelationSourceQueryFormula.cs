namespace Lattice.Core.Semantics;

/// <summary>
/// Requests the subjects connected to a known object by a relation expressed as a verb.
/// </summary>
public sealed record SemanticRelationSourceQueryFormula(
    string Object,
    string Verb) : SemanticFormula;
