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

public sealed record SemanticBooleanExpression(bool Value) : SemanticExpression;

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

/// <summary>
/// An access to one member of one value: the value that owns it and the member it exposes. The target is
/// any expression, so a member can be reached through a name, a call, or another member. The selected
/// language pack spells the separator.
/// </summary>
public sealed record SemanticMemberAccessExpression(
    SemanticExpression Target,
    string Member) : SemanticExpression;

/// <summary>
/// The argument the running program receives at one position, for example the directory a program was
/// started with. The selected language pack spells how a program reads its arguments.
/// </summary>
public sealed record SemanticProgramArgumentExpression(int Position) : SemanticExpression;

/// <summary>
/// Calls one method on a value the program already holds, for example asking a text value for its
/// trimmed form. The selected language pack spells how a member is reached and how a call is written,
/// so no requirement names a language construct.
/// </summary>
public sealed record SemanticMethodCallExpression(
    SemanticExpression Target,
    string Method,
    ImmutableArray<SemanticExpression> Arguments) : SemanticExpression;

/// <summary>
/// Converts one value to a target value kind, for example reading text as a number. The selected
/// language pack spells the conversion for each target kind, so no requirement names a language construct.
/// </summary>
public sealed record SemanticConversionExpression(
    SemanticValueKind Target,
    SemanticExpression Value) : SemanticExpression;

/// <summary>
/// One text value assembled from literal fragments and embedded values, for example a greeting around a
/// name. The selected language pack spells how a text value is built from its parts.
/// </summary>
public sealed record SemanticTextTemplateExpression(
    ImmutableArray<SemanticExpression> Parts) : SemanticExpression;

/// <summary>One field of a record literal: the field name and the value it holds.</summary>
public sealed record SemanticRecordEntry(string Name, SemanticExpression Value);

/// <summary>
/// A literal list of values, used for request-declared data a program holds, such as a list of records it
/// writes out. The selected language pack spells the brackets and the separators.
/// </summary>
public sealed record SemanticListLiteralExpression(
    ImmutableArray<SemanticExpression> Items) : SemanticExpression;

/// <summary>
/// A literal record of named fields, used for request-declared data a program holds. The selected language
/// pack spells the braces, the separators, and how one field name is written.
/// </summary>
public sealed record SemanticRecordLiteralExpression(
    ImmutableArray<SemanticRecordEntry> Entries) : SemanticExpression;

/// <summary>
/// A literal mapping from keys to values, used for request-declared dictionaries. Keys are request text and
/// stay strings; the selected language pack spells the braces, the separators, and how a key is quoted.
/// </summary>
public sealed record SemanticMapLiteralExpression(
    ImmutableArray<SemanticRecordEntry> Entries) : SemanticExpression;

/// <summary>
/// Several values reported side by side, in order, for a request that asks to print more than one thing at
/// once. The values are ordinary expressions; the separator between them is punctuation every target
/// language shares, so no pack field is needed.
/// </summary>
public sealed record SemanticArgumentsExpression(
    ImmutableArray<SemanticExpression> Values) : SemanticExpression;

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
        IsValid(expression) && IsValueCore(expression);

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

    /// <summary>
    /// Returns the variable references in an expression, in first-appearance order. These references
    /// are the request-derived data dependencies of the expression.
    /// </summary>
    public static ImmutableArray<string> GetReferencedNames(SemanticExpression expression)
    {
        if (!IsValid(expression))
        {
            return ImmutableArray<string>.Empty;
        }

        var names = ImmutableArray.CreateBuilder<string>();
        CollectReferencedNames(expression, names);
        return names.ToImmutable();
    }

    private static void CollectReferencedNames(
        SemanticExpression expression,
        ImmutableArray<string>.Builder names)
    {
        switch (expression)
        {
            case SemanticNameExpression name:
                if (!names.Any(existing => string.Equals(existing, name.Name, StringComparison.Ordinal)))
                {
                    names.Add(name.Name);
                }

                break;
            case SemanticFunctionCallExpression call:
                foreach (var argument in call.Arguments)
                {
                    CollectReferencedNames(argument, names);
                }

                break;
            case SemanticMemberAccessExpression member:
                CollectReferencedNames(member.Target, names);
                break;
            case SemanticMethodCallExpression methodCall:
                CollectReferencedNames(methodCall.Target, names);
                foreach (var argument in methodCall.Arguments)
                {
                    CollectReferencedNames(argument, names);
                }

                break;
            case SemanticConversionExpression conversion:
                CollectReferencedNames(conversion.Value, names);
                break;
            case SemanticTextTemplateExpression template:
                foreach (var part in template.Parts)
                {
                    CollectReferencedNames(part, names);
                }

                break;
            case SemanticListLiteralExpression list:
                foreach (var item in list.Items)
                {
                    CollectReferencedNames(item, names);
                }

                break;
            case SemanticRecordLiteralExpression record:
                foreach (var entry in record.Entries)
                {
                    CollectReferencedNames(entry.Value, names);
                }

                break;
            case SemanticUnaryExpression unary:
                CollectReferencedNames(unary.Operand, names);
                break;
            case SemanticBinaryExpression binary:
                CollectReferencedNames(binary.Left, names);
                CollectReferencedNames(binary.Right, names);
                break;
            case SemanticMapLiteralExpression map:
                foreach (var entry in map.Entries)
                {
                    CollectReferencedNames(entry.Value, names);
                }

                break;
            case SemanticArgumentsExpression arguments:
                foreach (var value in arguments.Values)
                {
                    CollectReferencedNames(value, names);
                }

                break;
        }
    }

    private static bool IsArithmeticCore(SemanticExpression expression) => expression switch
    {
        SemanticNameExpression => true,
        SemanticMemberAccessExpression => true,
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
        SemanticBooleanExpression => true,
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
            IsValueCore(binary.Left) && IsValueCore(binary.Right) &&
            AreKnownOperandTypesCompatible(binary.Left, binary.Right),
        _ => false
    };

    private static bool AreKnownOperandTypesCompatible(
        SemanticExpression left,
        SemanticExpression right)
    {
        var leftType = GetKnownValueType(left);
        var rightType = GetKnownValueType(right);
        return leftType is null || rightType is null || leftType == rightType;
    }

    private enum KnownValueType
    {
        Number,
        Text,
        Boolean
    }

    private static KnownValueType? GetKnownValueType(SemanticExpression expression) => expression switch
    {
        SemanticTextExpression => KnownValueType.Text,
        SemanticBooleanExpression => KnownValueType.Boolean,
        SemanticNumberExpression or SemanticUnaryExpression or SemanticBinaryExpression or SemanticFunctionCallExpression => KnownValueType.Number,
        SemanticConversionExpression conversion => conversion.Target switch
        {
            SemanticValueKind.Number => KnownValueType.Number,
            SemanticValueKind.Text => KnownValueType.Text,
            _ => null
        },
        SemanticTextTemplateExpression => KnownValueType.Text,
        SemanticProgramArgumentExpression => KnownValueType.Text,
        _ => null
    };

    private static bool IsValueCore(SemanticExpression expression) =>
        expression is SemanticTextExpression or SemanticBooleanExpression or
        SemanticListLiteralExpression or SemanticRecordLiteralExpression or
        SemanticMapLiteralExpression or SemanticArgumentsExpression ||
        expression is SemanticConversionExpression or SemanticTextTemplateExpression ||
        IsArithmeticCore(expression);

    private static bool ReferencesOnlyAvailableNamesCore(
        SemanticExpression expression,
        HashSet<string> availableNames,
        bool allowFunctionCalls,
        HashSet<string>? availableFunctions) => expression switch
    {
        SemanticNameExpression name => availableNames.Contains(name.Name),
        SemanticMemberAccessExpression member =>
            ReferencesOnlyAvailableNamesCore(
                member.Target, availableNames, allowFunctionCalls, availableFunctions),
        SemanticProgramArgumentExpression => true,
        SemanticNumberExpression or SemanticTextExpression or SemanticBooleanExpression => true,
        SemanticFunctionCallExpression call =>
            allowFunctionCalls &&
            (availableFunctions is null || availableFunctions.Contains(call.Name)) &&
            call.Arguments.All(argument => ReferencesOnlyAvailableNamesCore(
                argument, availableNames, allowFunctionCalls, availableFunctions)),
        SemanticMethodCallExpression methodCall =>
            ReferencesOnlyAvailableNamesCore(
                methodCall.Target, availableNames, allowFunctionCalls, availableFunctions) &&
            methodCall.Arguments.All(argument => ReferencesOnlyAvailableNamesCore(
                argument, availableNames, allowFunctionCalls, availableFunctions)),
        SemanticUnaryExpression unary =>
            ReferencesOnlyAvailableNamesCore(
                unary.Operand, availableNames, allowFunctionCalls, availableFunctions),
        SemanticConversionExpression conversion =>
            ReferencesOnlyAvailableNamesCore(
                conversion.Value, availableNames, allowFunctionCalls, availableFunctions),
        SemanticTextTemplateExpression template =>
            template.Parts.All(part => ReferencesOnlyAvailableNamesCore(
                part, availableNames, allowFunctionCalls, availableFunctions)),
        SemanticBinaryExpression binary =>
            ReferencesOnlyAvailableNamesCore(
                binary.Left, availableNames, allowFunctionCalls, availableFunctions) &&
            ReferencesOnlyAvailableNamesCore(
                binary.Right, availableNames, allowFunctionCalls, availableFunctions),
        SemanticListLiteralExpression list =>
            list.Items.All(item => ReferencesOnlyAvailableNamesCore(
                item, availableNames, allowFunctionCalls, availableFunctions)),
        SemanticRecordLiteralExpression record =>
            record.Entries.All(entry => ReferencesOnlyAvailableNamesCore(
                entry.Value, availableNames, allowFunctionCalls, availableFunctions)),
        SemanticMapLiteralExpression map =>
            map.Entries.All(entry => ReferencesOnlyAvailableNamesCore(
                entry.Value, availableNames, allowFunctionCalls, availableFunctions)),
        SemanticArgumentsExpression arguments =>
            arguments.Values.All(value => ReferencesOnlyAvailableNamesCore(
                value, availableNames, allowFunctionCalls, availableFunctions)),
        _ => false
    };

    private static bool ReferencesAnyNameCore(
        SemanticExpression expression,
        HashSet<string> names) => expression switch
    {
        SemanticNameExpression name => names.Contains(name.Name),
        SemanticMemberAccessExpression member => ReferencesAnyNameCore(member.Target, names),
        SemanticProgramArgumentExpression => false,
        SemanticFunctionCallExpression call =>
            call.Arguments.Any(argument => ReferencesAnyNameCore(argument, names)),
        SemanticMethodCallExpression methodCall =>
            ReferencesAnyNameCore(methodCall.Target, names) ||
            methodCall.Arguments.Any(argument => ReferencesAnyNameCore(argument, names)),
        SemanticUnaryExpression unary => ReferencesAnyNameCore(unary.Operand, names),
        SemanticConversionExpression conversion => ReferencesAnyNameCore(conversion.Value, names),
        SemanticTextTemplateExpression template =>
            template.Parts.Any(part => ReferencesAnyNameCore(part, names)),
        SemanticBinaryExpression binary =>
            ReferencesAnyNameCore(binary.Left, names) || ReferencesAnyNameCore(binary.Right, names),
        SemanticListLiteralExpression list => list.Items.Any(item => ReferencesAnyNameCore(item, names)),
        SemanticRecordLiteralExpression record =>
            record.Entries.Any(entry => ReferencesAnyNameCore(entry.Value, names)),
        SemanticMapLiteralExpression map =>
            map.Entries.Any(entry => ReferencesAnyNameCore(entry.Value, names)),
        SemanticArgumentsExpression arguments =>
            arguments.Values.Any(value => ReferencesAnyNameCore(value, names)),
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
            SemanticBooleanExpression => true,
            SemanticUnaryExpression unary =>
                Enum.IsDefined(unary.Operator) && IsValid(unary.Operand, depth + 1, ref nodeCount),
            SemanticBinaryExpression binary =>
                Enum.IsDefined(binary.Operator) &&
                IsValid(binary.Left, depth + 1, ref nodeCount) &&
                IsValid(binary.Right, depth + 1, ref nodeCount),
            SemanticFunctionCallExpression call => IsValidCallArguments(call, depth, ref nodeCount),
            SemanticMemberAccessExpression member =>
                IsValidIdentifier(member.Member) && IsValid(member.Target, depth + 1, ref nodeCount),
            SemanticProgramArgumentExpression programArgument =>
                programArgument.Position is >= 0 and <= 63,
            SemanticMethodCallExpression methodCall => IsValidMethodCall(methodCall, depth, ref nodeCount),
            SemanticConversionExpression conversion =>
                conversion.Target is SemanticValueKind.Number or SemanticValueKind.Text &&
                IsValid(conversion.Value, depth + 1, ref nodeCount),
            SemanticTextTemplateExpression template => IsValidTemplate(template, depth, ref nodeCount),
            SemanticListLiteralExpression list => IsValidList(list, depth, ref nodeCount),
            SemanticRecordLiteralExpression record => IsValidRecord(record, depth, ref nodeCount),
            SemanticMapLiteralExpression map => IsValidMap(map, depth, ref nodeCount),
            SemanticArgumentsExpression arguments => IsValidArguments(arguments, depth, ref nodeCount),
            _ => false
        };
    }

    private static bool IsValidMethodCall(
        SemanticMethodCallExpression call,
        int depth,
        ref int nodeCount)
    {
        if (!IsValidIdentifier(call.Method) ||
            call.Arguments.IsDefault ||
            call.Arguments.Length > 8 ||
            !IsValid(call.Target, depth + 1, ref nodeCount))
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

    private static bool IsValidList(
        SemanticListLiteralExpression list,
        int depth,
        ref int nodeCount)
    {
        if (list.Items.IsDefaultOrEmpty || list.Items.Length > 64)
        {
            return false;
        }

        foreach (var item in list.Items)
        {
            if (!IsValid(item, depth + 1, ref nodeCount))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidRecord(
        SemanticRecordLiteralExpression record,
        int depth,
        ref int nodeCount)
    {
        if (record.Entries.IsDefaultOrEmpty || record.Entries.Length > 64)
        {
            return false;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in record.Entries)
        {
            if (!IsValidIdentifier(entry.Name) ||
                !seen.Add(entry.Name) ||
                !IsValid(entry.Value, depth + 1, ref nodeCount))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidMap(
        SemanticMapLiteralExpression map,
        int depth,
        ref int nodeCount)
    {
        if (map.Entries.IsDefaultOrEmpty || map.Entries.Length > 64)
        {
            return false;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in map.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Name) ||
                entry.Name.Length > 128 ||
                !seen.Add(entry.Name) ||
                !IsValid(entry.Value, depth + 1, ref nodeCount))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidArguments(
        SemanticArgumentsExpression arguments,
        int depth,
        ref int nodeCount)
    {
        if (arguments.Values.IsDefaultOrEmpty || arguments.Values.Length > 16)
        {
            return false;
        }

        foreach (var value in arguments.Values)
        {
            if (!IsValid(value, depth + 1, ref nodeCount))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidTemplate(
        SemanticTextTemplateExpression template,
        int depth,
        ref int nodeCount)
    {
        if (template.Parts.IsDefaultOrEmpty || template.Parts.Length > 8)
        {
            return false;
        }

        foreach (var part in template.Parts)
        {
            if (!IsValid(part, depth + 1, ref nodeCount))
            {
                return false;
            }
        }

        return true;
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
            case SemanticMethodCallExpression methodCall:
                foreach (var argument in methodCall.Arguments)
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
            case SemanticConversionExpression conversion:
                CollectFunctionNames(conversion.Value, names);
                break;
            case SemanticTextTemplateExpression template:
                foreach (var part in template.Parts)
                {
                    CollectFunctionNames(part, names);
                }

                break;
            case SemanticListLiteralExpression list:
                foreach (var item in list.Items)
                {
                    CollectFunctionNames(item, names);
                }

                break;
            case SemanticRecordLiteralExpression record:
                foreach (var entry in record.Entries)
                {
                    CollectFunctionNames(entry.Value, names);
                }

                break;
        }
    }
}
