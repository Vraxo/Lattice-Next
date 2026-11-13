using System.Collections.Immutable;

namespace Lattice.Core.Semantics;

/// <summary>
/// Asks which of a finite set of named choices is a member of a requested category.
/// </summary>
public sealed record ChoiceQuestionFormula(
    SemanticIdentifier Category,
    ImmutableArray<SemanticIdentifier> Choices) : SemanticFormula;
