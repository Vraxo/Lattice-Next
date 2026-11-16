using System.Collections.Immutable;

namespace Lattice.Core.Programming;

/// <summary>
/// Lowers a language-neutral program plan into the Python syntax model.
/// </summary>
public static class PythonProgramLowerer
{
    public static PythonProgram Lower(ProgramPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.Statements.IsDefault)
        {
            throw new ArgumentException("Program plan statements must be initialized.", nameof(plan));
        }

        return new PythonProgram(LowerStatements(plan.Statements));
    }

    private static ImmutableArray<PythonStatement> LowerStatements(
        ImmutableArray<ProgramStatementPlan> statements)
    {
        var builder = ImmutableArray.CreateBuilder<PythonStatement>(statements.Length);
        foreach (var statement in statements)
        {
            builder.Add(LowerStatement(statement));
        }

        return builder.ToImmutable();
    }

    private static PythonStatement LowerStatement(ProgramStatementPlan statement)
    {
        return statement switch
        {
            ImportModulePlan import => new PythonImportStatement(import.ModuleName),
            AssignValuePlan assignment => new PythonAssignmentStatement(
                LowerExpression(assignment.Target),
                LowerExpression(assignment.Value)),
            EvaluateExpressionPlan expression => new PythonExpressionStatement(
                LowerExpression(expression.Expression)),
            ReturnValuePlan result => new PythonReturnStatement(
                result.Value is null ? null : LowerExpression(result.Value)),
            IterateValuesPlan loop => new PythonForStatement(
                new PythonNameExpression(loop.Variable),
                LowerExpression(loop.Values),
                LowerStatements(loop.Body)),
            ConditionalPlan conditional => new PythonIfStatement(
                LowerExpression(conditional.Condition),
                LowerStatements(conditional.WhenTrue),
                LowerStatements(conditional.WhenFalse)),
            DefineFunctionPlan function => new PythonFunctionDefinitionStatement(
                function.Name,
                function.Parameters,
                LowerStatements(function.Body)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(statement),
                statement,
                "Unsupported language-neutral program statement.")
        };
    }

    private static PythonExpression LowerExpression(ProgramExpressionPlan expression)
    {
        return expression switch
        {
            VariablePlan variable => new PythonNameExpression(variable.Name),
            TextValuePlan text => new PythonStringExpression(text.Value),
            NumberValuePlan number => new PythonNumberExpression(number.Value),
            BooleanValuePlan boolean => new PythonBooleanExpression(boolean.Value),
            SequenceValuePlan sequence => new PythonListExpression(
                LowerExpressions(sequence.Values)),
            MappingValuePlan mapping => new PythonDictionaryExpression(
                mapping.Entries.Select(entry => new PythonDictionaryEntry(
                    LowerExpression(entry.Key),
                    LowerExpression(entry.Value))).ToImmutableArray()),
            MemberAccessPlan member => new PythonAttributeExpression(
                LowerExpression(member.Target),
                member.Member),
            IndexAccessPlan index => new PythonSubscriptExpression(
                LowerExpression(index.Target),
                LowerExpression(index.Index)),
            InvokeOperationPlan invocation => new PythonCallExpression(
                LowerExpression(invocation.Target),
                LowerExpressions(invocation.Arguments),
                invocation.NamedArguments.Select(argument => new PythonKeywordArgument(
                    argument.Name,
                    LowerExpression(argument.Value))).ToImmutableArray()),
            CombineValuesPlan binary => new PythonBinaryExpression(
                LowerExpression(binary.Left),
                LowerOperator(binary.Operator),
                LowerExpression(binary.Right)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(expression),
                expression,
                "Unsupported language-neutral program expression.")
        };
    }

    private static ImmutableArray<PythonExpression> LowerExpressions(
        ImmutableArray<ProgramExpressionPlan> expressions)
    {
        var builder = ImmutableArray.CreateBuilder<PythonExpression>(expressions.Length);
        foreach (var expression in expressions)
        {
            builder.Add(LowerExpression(expression));
        }

        return builder.ToImmutable();
    }

    private static PythonBinaryOperator LowerOperator(ProgramBinaryOperator operation)
    {
        return operation switch
        {
            ProgramBinaryOperator.Add => PythonBinaryOperator.Add,
            ProgramBinaryOperator.Subtract => PythonBinaryOperator.Subtract,
            ProgramBinaryOperator.Multiply => PythonBinaryOperator.Multiply,
            ProgramBinaryOperator.Divide => PythonBinaryOperator.Divide,
            ProgramBinaryOperator.Modulo => PythonBinaryOperator.Modulo,
            ProgramBinaryOperator.Equal => PythonBinaryOperator.Equal,
            ProgramBinaryOperator.NotEqual => PythonBinaryOperator.NotEqual,
            ProgramBinaryOperator.LessThan => PythonBinaryOperator.LessThan,
            ProgramBinaryOperator.LessThanOrEqual => PythonBinaryOperator.LessThanOrEqual,
            ProgramBinaryOperator.GreaterThan => PythonBinaryOperator.GreaterThan,
            ProgramBinaryOperator.GreaterThanOrEqual => PythonBinaryOperator.GreaterThanOrEqual,
            ProgramBinaryOperator.And => PythonBinaryOperator.And,
            ProgramBinaryOperator.Or => PythonBinaryOperator.Or,
            _ => throw new ArgumentOutOfRangeException(
                nameof(operation),
                operation,
                "Unsupported language-neutral binary operator.")
        };
    }
}
