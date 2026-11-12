using System.Collections.Immutable;

namespace Lattice.Core.Semantics;

/// <summary>
/// Requests the definition of a concept, optionally narrowed by descriptive modifiers.
/// </summary>
public sealed record DefinitionQuestionFormula(
    SemanticIdentifier Concept,
    ImmutableArray<SemanticIdentifier> Modifiers) : SemanticFormula;
