using System.Collections.Immutable;

namespace Lattice.Core.Semantics;

/// <summary>
/// Selects the listed item types that are explicitly represented as used for an activity.
/// </summary>
public sealed record ActivitySelectionFormula(
    SemanticIdentifier Activity,
    ImmutableArray<SemanticIdentifier> Items) : SemanticFormula;
