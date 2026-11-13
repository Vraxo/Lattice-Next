using System.Collections.Immutable;

namespace Lattice.Core.Semantics;

public sealed record TaskRequestFormula(
    TaskRequestAction Action,
    string Subject,
    string? Context,
    string? OutputFormat,
    string? Scope,
    ImmutableArray<string> Requirements) : SemanticFormula;
