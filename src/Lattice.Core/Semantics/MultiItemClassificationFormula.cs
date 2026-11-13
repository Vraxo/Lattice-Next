using System.Collections.Immutable;

namespace Lattice.Core.Semantics;

/// <summary>
/// Classifies each item in a finite list into exactly one of the requested categories.
/// </summary>
public sealed record MultiItemClassificationFormula(
    SemanticIdentifier ItemType,
    ImmutableArray<SemanticIdentifier> Categories,
    ImmutableArray<SemanticIdentifier> Items) : SemanticFormula;
