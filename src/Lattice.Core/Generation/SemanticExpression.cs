using System.Collections.Immutable;

namespace Lattice.Core.Generation;

public enum SemanticExpressionOperator
{
    Add,
    Subtract,
    Multiply,
    Divide,
    Remainder,
    Equal,
    NotEqual,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual,
    And,
    Or,
    Not,
    Positive,
    Negative
}

/// <summary>A language-neutral expression node. Syntax is supplied by a selected language pack.</summary>
public abstract record SemanticExpression;

public sealed record SemanticNameExpression(string Name) : SemanticExpression;

/// <summary>A non-negative invariant decimal number, using ASCII digits and an optional dot.</summary>
public sealed record SemanticNumberExpression(string Value) : SemanticExpression;

public sealed record SemanticTextExpression(string Value) : SemanticExpression;

public sealed record SemanticUnaryExpression(
    SemanticExpressionOperator Operator,
    SemanticExpression Operand) : SemanticExpression;

public sealed record SemanticBinaryExpression(
    SemanticExpressionOperator Operator,
    SemanticExpression Left,
    SemanticExpression Right) : SemanticExpression;

public sealed record SemanticFunctionCallExpression(
    string Name,
    ImmutableArray<SemanticExpression> Arguments) : SemanticExpression;

internal static class SemanticExpressionAnalysis
{
    private const int MaximumDepth = 64;
    private const int MaximumNodes = 2048;
    private const int MaximumValueLength = 16_384;

    public static bool IsValid(SemanticExpression? expression)
    {
        var nodeCount = 0;
        return IsValid(expression, depth: 0, ref nodeCount);
    }

    public static bool IsArithmetic(SemanticExpression expression) =>
        IsValid(expression) && IsArithmeticCore(expression);

    public static bool IsValue(SemanticExpression expression) =>
        IsValid(expression) &&
        (expression is SemanticTextExpression || IsArithmeticCore(expression));

    public static bool IsBoolean(SemanticExpression expression) =>
        IsValid(expression) && IsBooleanCore(expression);

    public static bool ReferencesOnlyAvailableNames(
        SemanticExpression expression,
        HashSet<string> availableNames,
        bool allowFunctionCalls = false,
        HashSet<string>? availableFunctions = null) =>
        IsValid(expression) &&
        ReferencesOnlyAvailableNamesCore(expression, availableNames, allowFunctionCalls, availableFunctions);

    public static bool ReferencesAnyName(
        SemanticExpression expression,
        IEnumerable<string> names,
        StringComparer? comparer = null)
    {
        if (!IsValid(expression))
        {
            return false;
        }

        var available = names.ToHashSet(comparer ?? StringComparer.OrdinalIgnoreCase);
        return ReferencesAnyNameCore(expression, available);
    }

    public static ImmutableArray<string> GetFunctionNames(SemanticExpression expression)
    {
        if (!IsValid(expression))
        {
            return ImmutableArray<string>.Empty;
        }

        var names = ImmutableArray.CreateBuilder<string>();
        CollectFunctionNames(expression, names);
        return names.ToImmutable();
    }

    private static bool IsArithmeticCore(SemanticExpression expression) => expression switch
    {
        SemanticNameExpression => true,
        SemanticNumberExpression => true,
        SemanticFunctionCallExpression call => call.Arguments.All(IsArithmeticCore),
        SemanticUnaryExpression unary when unary.Operator is
            SemanticExpressionOperator.Positive or SemanticExpressionOperator.Negative =>
            IsArithmeticCore(unary.Operand),
        SemanticBinaryExpression binary when binary.Operator is
            SemanticExpressionOperator.Add or
            SemanticExpressionOperator.Subtract or
            SemanticExpressionOperator.Multiply or
            SemanticExpressionOperator.Divide or
            SemanticExpressionOperator.Remainder =>
            IsArithmeticCore(binary.Left) && IsArithmeticCore(binary.Right),
        _ => false
    };

    private static bool IsBooleanCore(SemanticExpression expression) => expression switch
    {
        SemanticUnaryExpression { Operator: SemanticExpressionOperator.Not } unary =>
            IsBooleanCore(unary.Operand),
        SemanticBinaryExpression { Operator: SemanticExpressionOperator.And or SemanticExpressionOperator.Or } binary =>
            IsBooleanCore(binary.Left) && IsBooleanCore(binary.Right),
        SemanticBinaryExpression { Operator: SemanticExpressionOperator.Equal or
            SemanticExpressionOperator.NotEqual or
            SemanticExpressionOperator.LessThan or
            SemanticExpressionOperator.LessThanOrEqual or
            SemanticExpressionOperator.GreaterThan or
            SemanticExpressionOperator.GreaterThanOrEqual } binary =>
            IsValueCore(binary.Left) && IsValueCore(binary.Right),
        _ => false
    };

    private static bool IsValueCore(SemanticExpression expression) =>
        expression is SemanticTextExpression || IsArithmeticCore(expression);

    private static bool ReferencesOnlyAvailableNamesCore(
        SemanticExpression expression,
        HashSet<string> availableNames,
        bool allowFunctionCalls,
        HashSet<string>? availableFunctions) => expression switch
    {
        SemanticNameExpression name => availableNames.Contains(name.Name),
        SemanticNumberExpression or SemanticTextExpression => true,
        SemanticFunctionCallExpression call =>
            allowFunctionCalls &&
            (availableFunctions is null || availableFunctions.Contains(call.Name)) &&
            call.Arguments.All(argument => ReferencesOnlyAvailableNamesCore(
                argument, availableNames, allowFunctionCalls, availableFunctions)),
        SemanticUnaryExpression unary =>
            ReferencesOnlyAvailableNamesCore(
                unary.Operand, availableNames, allowFunctionCalls, availableFunctions),
        SemanticBinaryExpression binary =>
            ReferencesOnlyAvailableNamesCore(
                binary.Left, availableNames, allowFunctionCalls, availableFunctions) &&
            ReferencesOnlyAvailableNamesCore(
                binary.Right, availableNames, allowFunctionCalls, availableFunctions),
        _ => false
    };

    private static bool ReferencesAnyNameCore(
        SemanticExpression expression,
        HashSet<string> names) => expression switch
    {
        SemanticNameExpression name => names.Contains(name.Name),
        SemanticFunctionCallExpression call =>
            call.Arguments.Any(argument => ReferencesAnyNameCore(argument, names)),
        SemanticUnaryExpression unary => ReferencesAnyNameCore(unary.Operand, names),
        SemanticBinaryExpression binary =>
            ReferencesAnyNameCore(binary.Left, names) || ReferencesAnyNameCore(binary.Right, names),
        _ => false
    };

    private static bool IsValid(SemanticExpression? expression, int depth, ref int nodeCount)
    {
        nodeCount++;
        if (expression is null || depth > MaximumDepth || nodeCount > MaximumNodes)
        {
            return false;
        }

        return expression switch
        {
            SemanticNameExpression name => IsValidIdentifier(name.Name),
            SemanticNumberExpression number => IsValidCanonicalNumber(number.Value),
            SemanticTextExpression text => text.Value is not null && text.Value.Length <= MaximumValueLength,
            SemanticUnaryExpression unary =>
                Enum.IsDefined(unary.Operator) && IsValid(unary.Operand, depth + 1, ref nodeCount),
            SemanticBinaryExpression binary =>
                Enum.IsDefined(binary.Operator) &&
                IsValid(binary.Left, depth + 1, ref nodeCount) &&
                IsValid(binary.Right, depth + 1, ref nodeCount),
            SemanticFunctionCallExpression call => IsValidCallArguments(call, depth, ref nodeCount),
            _ => false
        };
    }

    private static bool IsValidCallArguments(
        SemanticFunctionCallExpression call,
        int depth,
        ref int nodeCount)
    {
        if (!IsValidIdentifier(call.Name) || call.Arguments.IsDefault || call.Arguments.Length > 128)
        {
            return false;
        }

        foreach (var argument in call.Arguments)
        {
            if (!IsValid(argument, depth + 1, ref nodeCount))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidCanonicalNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
        {
            return false;
        }

        var decimalPoint = value.IndexOf('.');
        if (decimalPoint < 0)
        {
            return value.All(char.IsAsciiDigit);
        }

        return decimalPoint > 0 &&
            decimalPoint < value.Length - 1 &&
            value.IndexOf('.', decimalPoint + 1) < 0 &&
            value[..decimalPoint].All(char.IsAsciiDigit) &&
            value[(decimalPoint + 1)..].All(char.IsAsciiDigit);
    }

    private static bool IsValidIdentifier(string? identifier) =>
        !string.IsNullOrWhiteSpace(identifier) && identifier.Length <= 128;

    private static void CollectFunctionNames(
        SemanticExpression expression,
        ImmutableArray<string>.Builder names)
    {
        switch (expression)
        {
            case SemanticFunctionCallExpression call:
                names.Add(call.Name);
                foreach (var argument in call.Arguments)
                {
                    CollectFunctionNames(argument, names);
                }
                break;
            case SemanticUnaryExpression unary:
                CollectFunctionNames(unary.Operand, names);
                break;
            case SemanticBinaryExpression binary:
                CollectFunctionNames(binary.Left, names);
                CollectFunctionNames(binary.Right, names);
                break;
        }
    }
}
