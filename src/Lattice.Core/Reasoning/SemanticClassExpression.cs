using System.Collections.Immutable;

namespace Lattice.Core.Reasoning;

public sealed record SemanticClassExpression(
    SemanticClassExpressionKind Kind,
    string? Relation = null,
    string? TargetType = null,
    ImmutableArray<SemanticClassExpression> Operands = default,
    SemanticClassExpression? Filler = null,
    string? StartRelation = null,
    int MinimumLength = 0);
