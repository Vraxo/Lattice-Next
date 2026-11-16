using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Lattice.Core.Programming;

public sealed record PythonProgram(ImmutableArray<PythonStatement> Statements);

public abstract record PythonStatement;

public sealed record PythonImportStatement(string ModuleName) : PythonStatement;

public sealed record PythonAssignmentStatement(
    PythonExpression Target,
    PythonExpression Value) : PythonStatement;

public sealed record PythonExpressionStatement(PythonExpression Expression) : PythonStatement;

public sealed record PythonIfStatement(
    PythonExpression Condition,
    ImmutableArray<PythonStatement> Body,
    ImmutableArray<PythonStatement> ElseBody) : PythonStatement;

public sealed record PythonForStatement(
    PythonNameExpression Iterator,
    PythonExpression Iterable,
    ImmutableArray<PythonStatement> Body) : PythonStatement;

public sealed record PythonFunctionDefinitionStatement(
    string Name,
    ImmutableArray<string> Parameters,
    ImmutableArray<PythonStatement> Body) : PythonStatement;

public sealed record PythonReturnStatement(PythonExpression? Value) : PythonStatement;

public abstract record PythonExpression;

public sealed record PythonNameExpression(string Name) : PythonExpression;

public sealed record PythonStringExpression(string Value) : PythonExpression;

public sealed record PythonNumberExpression(double Value) : PythonExpression;

public sealed record PythonBooleanExpression(bool Value) : PythonExpression;

public sealed record PythonListExpression(ImmutableArray<PythonExpression> Items) : PythonExpression;

public sealed record PythonDictionaryExpression(
    ImmutableArray<PythonDictionaryEntry> Entries) : PythonExpression;

public sealed record PythonDictionaryEntry(
    PythonExpression Key,
    PythonExpression Value);

public sealed record PythonAttributeExpression(
    PythonExpression Target,
    string AttributeName) : PythonExpression;

public sealed record PythonSubscriptExpression(
    PythonExpression Target,
    PythonExpression Index) : PythonExpression;

public sealed record PythonCallExpression(
    PythonExpression Target,
    ImmutableArray<PythonExpression> Arguments,
    ImmutableArray<PythonKeywordArgument> KeywordArguments) : PythonExpression;

public sealed record PythonKeywordArgument(string Name, PythonExpression Value);

public sealed record PythonBinaryExpression(
    PythonExpression Left,
    PythonBinaryOperator Operator,
    PythonExpression Right) : PythonExpression;

public enum PythonBinaryOperator
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

public static class PythonCodeRenderer
{
    private static readonly Regex IdentifierPattern = new(
        @"^[A-Za-z_][A-Za-z0-9_]*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string Render(PythonProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);
        if (program.Statements.IsDefault)
        {
            throw new ArgumentException("Program statements must be initialized.", nameof(program));
        }

        var builder = new StringBuilder();
        AppendStatements(builder, program.Statements, 0);
        return builder.ToString();
    }

    private static void AppendStatements(
        StringBuilder builder,
        ImmutableArray<PythonStatement> statements,
        int indentation)
    {
        foreach (var statement in statements)
        {
            AppendIndent(builder, indentation);
            AppendStatement(builder, statement, indentation);
            if (statement is not (PythonIfStatement or PythonForStatement or PythonFunctionDefinitionStatement))
            {
                builder.AppendLine();
            }
        }
    }

    private static void AppendStatement(
        StringBuilder builder,
        PythonStatement statement,
        int indentation)
    {
        switch (statement)
        {
            case PythonImportStatement import:
                ValidateModuleName(import.ModuleName);
                builder.Append("import ").Append(import.ModuleName);
                break;

            case PythonAssignmentStatement assignment:
                if (!IsAssignable(assignment.Target))
                {
                    throw new ArgumentException("The assignment target is not assignable.");
                }

                AppendExpression(builder, assignment.Target);
                builder.Append(" = ");
                AppendExpression(builder, assignment.Value);
                break;

            case PythonExpressionStatement expression:
                AppendExpression(builder, expression.Expression);
                break;

            case PythonIfStatement conditional:
                builder.Append("if ");
                AppendExpression(builder, conditional.Condition);
                builder.Append(':');
                AppendBlock(builder, conditional.Body, indentation);
                if (!conditional.ElseBody.IsDefaultOrEmpty)
                {
                    AppendIndent(builder, indentation);
                    builder.AppendLine("else:");
                    AppendStatements(builder, conditional.ElseBody, indentation + 1);
                }
                break;

            case PythonForStatement loop:
                ValidateIdentifier(loop.Iterator.Name);
                builder.Append("for ").Append(loop.Iterator.Name).Append(" in ");
                AppendExpression(builder, loop.Iterable);
                builder.Append(':');
                AppendBlock(builder, loop.Body, indentation);
                break;

            case PythonFunctionDefinitionStatement function:
                ValidateIdentifier(function.Name);
                ValidateIdentifiers(function.Parameters);
                if (function.Parameters.Distinct(StringComparer.Ordinal).Count() != function.Parameters.Length)
                {
                    throw new ArgumentException("Function parameters must be unique.");
                }

                builder.Append("def ").Append(function.Name).Append('(')
                    .Append(string.Join(", ", function.Parameters)).Append("):");
                AppendBlock(builder, function.Body, indentation);
                break;

            case PythonReturnStatement result:
                builder.Append("return");
                if (result.Value is not null)
                {
                    builder.Append(' ');
                    AppendExpression(builder, result.Value);
                }
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(statement),
                    statement,
                    "Unsupported Python statement node.");
        }
    }

    private static void AppendBlock(
        StringBuilder builder,
        ImmutableArray<PythonStatement> statements,
        int indentation)
    {
        builder.AppendLine();
        if (statements.IsDefaultOrEmpty)
        {
            AppendIndent(builder, indentation + 1);
            builder.AppendLine("pass");
            return;
        }

        AppendStatements(builder, statements, indentation + 1);
    }

    private static void AppendExpression(StringBuilder builder, PythonExpression expression)
    {
        switch (expression)
        {
            case PythonNameExpression name:
                ValidateIdentifier(name.Name);
                builder.Append(name.Name);
                break;

            case PythonStringExpression text:
                builder.Append(JsonSerializer.Serialize(text.Value));
                break;

            case PythonNumberExpression number:
                if (!double.IsFinite(number.Value))
                {
                    throw new ArgumentOutOfRangeException(nameof(expression), "Python numeric literals must be finite.");
                }

                builder.Append(number.Value.ToString("R", CultureInfo.InvariantCulture));
                break;

            case PythonBooleanExpression boolean:
                builder.Append(boolean.Value ? "True" : "False");
                break;

            case PythonListExpression list:
                AppendDelimited(builder, list.Items, '[', ']');
                break;

            case PythonDictionaryExpression dictionary:
                builder.Append('{');
                for (var index = 0; index < dictionary.Entries.Length; index++)
                {
                    if (index > 0)
                    {
                        builder.Append(", ");
                    }

                    AppendExpression(builder, dictionary.Entries[index].Key);
                    builder.Append(": ");
                    AppendExpression(builder, dictionary.Entries[index].Value);
                }
                builder.Append('}');
                break;

            case PythonAttributeExpression attribute:
                AppendExpression(builder, attribute.Target);
                builder.Append('.');
                ValidateIdentifier(attribute.AttributeName);
                builder.Append(attribute.AttributeName);
                break;

            case PythonSubscriptExpression subscript:
                AppendExpression(builder, subscript.Target);
                builder.Append('[');
                AppendExpression(builder, subscript.Index);
                builder.Append(']');
                break;

            case PythonCallExpression call:
                AppendExpression(builder, call.Target);
                builder.Append('(');
                var hasArgument = false;
                foreach (var argument in call.Arguments)
                {
                    if (hasArgument)
                    {
                        builder.Append(", ");
                    }

                    AppendExpression(builder, argument);
                    hasArgument = true;
                }

                foreach (var argument in call.KeywordArguments)
                {
                    if (hasArgument)
                    {
                        builder.Append(", ");
                    }

                    ValidateIdentifier(argument.Name);
                    builder.Append(argument.Name).Append('=');
                    AppendExpression(builder, argument.Value);
                    hasArgument = true;
                }

                builder.Append(')');
                break;

            case PythonBinaryExpression binary:
                builder.Append('(');
                AppendExpression(builder, binary.Left);
                builder.Append(' ').Append(GetOperator(binary.Operator)).Append(' ');
                AppendExpression(builder, binary.Right);
                builder.Append(')');
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(expression),
                    expression,
                    "Unsupported Python expression node.");
        }
    }

    private static void AppendDelimited(
        StringBuilder builder,
        ImmutableArray<PythonExpression> expressions,
        char opening,
        char closing)
    {
        builder.Append(opening);
        for (var index = 0; index < expressions.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            AppendExpression(builder, expressions[index]);
        }

        builder.Append(closing);
    }

    private static bool IsAssignable(PythonExpression expression)
    {
        return expression is PythonNameExpression or PythonAttributeExpression or PythonSubscriptExpression;
    }

    private static string GetOperator(PythonBinaryOperator operation)
    {
        return operation switch
        {
            PythonBinaryOperator.Add => "+",
            PythonBinaryOperator.Subtract => "-",
            PythonBinaryOperator.Multiply => "*",
            PythonBinaryOperator.Divide => "/",
            PythonBinaryOperator.Modulo => "%",
            PythonBinaryOperator.Equal => "==",
            PythonBinaryOperator.NotEqual => "!=",
            PythonBinaryOperator.LessThan => "<",
            PythonBinaryOperator.LessThanOrEqual => "<=",
            PythonBinaryOperator.GreaterThan => ">",
            PythonBinaryOperator.GreaterThanOrEqual => ">=",
            PythonBinaryOperator.And => "and",
            PythonBinaryOperator.Or => "or",
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unsupported Python operator.")
        };
    }

    private static void ValidateModuleName(string moduleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        foreach (var segment in moduleName.Split('.'))
        {
            ValidateIdentifier(segment);
        }
    }

    private static void ValidateIdentifiers(ImmutableArray<string> identifiers)
    {
        if (identifiers.IsDefault)
        {
            throw new ArgumentException("Identifiers must be initialized.", nameof(identifiers));
        }

        foreach (var identifier in identifiers)
        {
            ValidateIdentifier(identifier);
        }
    }

    private static void ValidateIdentifier(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        if (!IdentifierPattern.IsMatch(identifier))
        {
            throw new ArgumentException($"'{identifier}' is not a supported Python identifier.", nameof(identifier));
        }
    }

    private static void AppendIndent(StringBuilder builder, int indentation)
    {
        builder.Append(' ', indentation * 4);
    }
}
