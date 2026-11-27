using System.Collections.Immutable;

namespace Lattice.Core.Semantics;

public sealed record TaskRequestFormula(
    TaskRequestAction Action,
    string Subject,
    string? Context,
    string? OutputFormat,
    string? Scope,
    ImmutableArray<string> Requirements) : SemanticFormula
{
    /// <summary>
    /// The request phrase as it was written, when the parse kept it. A task the knowledge models only through a
    /// form with slots is recognised from this phrase; the normalised subject alone cannot carry its parts.
    /// </summary>
    public string? Phrase { get; init; }
}
