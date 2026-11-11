namespace Lattice.Core.Semantics;

public sealed record ObjectiveCondition(
    string Name,
    ObjectiveConditionRole Role,
    ObjectiveConditionOperator Operator,
    ObjectiveConditionValue? Value,
    string SourceText);
