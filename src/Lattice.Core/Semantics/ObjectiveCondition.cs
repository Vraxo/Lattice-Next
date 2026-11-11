namespace Lattice.Core.Semantics;

public sealed record ObjectiveCondition(
    string Name,
    ObjectiveConditionRole Role,
    ObjectiveConditionOperator Operator,
    ObjectiveConditionValue? Value,
    string SourceText)
{
    public ObjectiveConditionValueKind ExpectedValueKind { get; init; }

    public string? MissingValuePrompt { get; init; }

    public string? ValueSourceText { get; init; }
}
