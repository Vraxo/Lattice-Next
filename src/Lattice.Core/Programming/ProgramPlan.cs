using System.Collections.Immutable;

namespace Lattice.Core.Programming;

/// <summary>
/// Describes a program without committing to a target programming language.
/// </summary>
public sealed record ProgramPlan(ImmutableArray<ProgramStatementPlan> Statements);

public abstract record ProgramStatementPlan;

public sealed record ImportModulePlan(string ModuleName) : ProgramStatementPlan;

public sealed record AssignValuePlan(ProgramExpressionPlan Target, ProgramExpressionPlan Value)
    : ProgramStatementPlan;

public sealed record EvaluateExpressionPlan(ProgramExpressionPlan Expression) : ProgramStatementPlan;

public sealed record ReturnValuePlan(ProgramExpressionPlan? Value) : ProgramStatementPlan;

public sealed record IterateValuesPlan(
    string Variable,
    ProgramExpressionPlan Values,
    ImmutableArray<ProgramStatementPlan> Body) : ProgramStatementPlan;

public sealed record ConditionalPlan(
    ProgramExpressionPlan Condition,
    ImmutableArray<ProgramStatementPlan> WhenTrue,
    ImmutableArray<ProgramStatementPlan> WhenFalse) : ProgramStatementPlan;

public sealed record DefineFunctionPlan(
    string Name,
    ImmutableArray<string> Parameters,
    ImmutableArray<ProgramStatementPlan> Body) : ProgramStatementPlan;

public abstract record ProgramExpressionPlan;

public sealed record VariablePlan(string Name) : ProgramExpressionPlan;

public sealed record TextValuePlan(string Value) : ProgramExpressionPlan;

public sealed record NumberValuePlan(double Value) : ProgramExpressionPlan;

public sealed record BooleanValuePlan(bool Value) : ProgramExpressionPlan;

public sealed record SequenceValuePlan(ImmutableArray<ProgramExpressionPlan> Values)
    : ProgramExpressionPlan;

public sealed record MappingValuePlan(
    ImmutableArray<MappingEntryPlan> Entries) : ProgramExpressionPlan;

public sealed record MappingEntryPlan(ProgramExpressionPlan Key, ProgramExpressionPlan Value);

public sealed record MemberAccessPlan(ProgramExpressionPlan Target, string Member)
    : ProgramExpressionPlan;

public sealed record IndexAccessPlan(ProgramExpressionPlan Target, ProgramExpressionPlan Index)
    : ProgramExpressionPlan;

public sealed record InvokeOperationPlan(
    ProgramExpressionPlan Target,
    ImmutableArray<ProgramExpressionPlan> Arguments,
    ImmutableArray<OperationArgumentPlan> NamedArguments) : ProgramExpressionPlan;

public sealed record OperationArgumentPlan(string Name, ProgramExpressionPlan Value);

public sealed record CombineValuesPlan(
    ProgramExpressionPlan Left,
    ProgramBinaryOperator Operator,
    ProgramExpressionPlan Right) : ProgramExpressionPlan;

public enum ProgramBinaryOperator
{
    Add,
    Subtract,
    Multiply,
    Divide,
    Modulo,
    Equal,
    NotEqual,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual,
    And,
    Or
}
