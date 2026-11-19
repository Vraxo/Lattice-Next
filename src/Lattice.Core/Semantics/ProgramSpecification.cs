using System.Collections.Immutable;

namespace Lattice.Core.Semantics;

public sealed record ProgramSpecification(
    ImmutableArray<ProgramStatement> Statements);

public abstract record ProgramStatement;

public sealed record ProgramAssignmentStatement(
    string Target,
    ProgramExpression Value) : ProgramStatement;

public sealed record ProgramOutputStatement(
    ProgramExpression Value) : ProgramStatement;

public abstract record ProgramExpression;

public sealed record ProgramNameExpression(string Name) : ProgramExpression;

public sealed record ProgramNumberLiteral(string Value) : ProgramExpression;

public sealed record ProgramStringLiteral(string Value) : ProgramExpression;

public sealed record ProgramBinaryExpression(
    ProgramExpression Left,
    ProgramBinaryOperator Operator,
    ProgramExpression Right) : ProgramExpression;

public sealed record ProgramUnaryExpression(
    ProgramUnaryOperator Operator,
    ProgramExpression Operand) : ProgramExpression;

public enum ProgramBinaryOperator
{
    Add,
    Subtract,
    Multiply,
    Divide,
    Remainder
}

public enum ProgramUnaryOperator
{
    Negate
}
