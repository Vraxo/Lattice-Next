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
    public ImmutableArray<TaskRequestStep> GenerationSteps { get; init; } =
        ImmutableArray<TaskRequestStep>.Empty;
}

public sealed record TaskRequestStep(
    string Effect,
    string ArgumentKind,
    string ArgumentValue);
