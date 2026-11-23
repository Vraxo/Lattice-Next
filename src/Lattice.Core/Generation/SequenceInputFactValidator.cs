using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Lattice.Core.Generation;

public static class SequenceInputFactValidator
{
    private static readonly Regex IdentifierPattern = new(
        @"^[A-Za-z_][A-Za-z0-9_]*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex NumberPattern = new(
        @"^[+-]?\d+(?:\.\d+)?$",
        RegexOptions.CultureInvariant);

    private static readonly Regex FunctionCallPattern = new(
        @"^\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(\s*(?<arguments>[^()]*)\s*\)\s*$",
        RegexOptions.CultureInvariant);

    public static bool TryValidate(
        ImmutableArray<SequenceInputFactValidation> validations,
        ImmutableArray<SequenceSemanticFact> facts,
        out string? error)
    {
        error = null;
        if (validations.IsDefaultOrEmpty)
        {
            return true;
        }

        var factMap = facts.ToDictionary(fact => fact.Id, StringComparer.Ordinal);
        foreach (var validation in validations)
        {
            if (validation.AppliesWhenFactId is { } appliesWhenFactId &&
                !factMap.ContainsKey(appliesWhenFactId))
            {
                continue;
            }

            if (!factMap.ContainsKey(validation.FactId))
            {
                error = $"Semantic input validation '{validation.Id}' is missing its required source fact.";
                return false;
            }

            var valid = validation.Kind switch
            {
                "identifierValue" => TryGetValue(factMap, validation.FactId, validation.ValueId, out var value) &&
                    IsIdentifier(value),
                "integerValue" => TryGetValue(factMap, validation.FactId, validation.ValueId, out var integerValue) &&
                    int.TryParse(integerValue, NumberStyles.None, CultureInfo.InvariantCulture, out _),
                "ascendingIntegerRange" => TryValidateAscendingIntegerRange(factMap, validation),
                "pythonListLiteral" => TryGetValue(factMap, validation.FactId, validation.ValueId, out var listLiteral) &&
                    IsPythonListLiteral(listLiteral),
                "identifierCollection" => TryGetCollection(factMap, validation.FactId, validation.ValueId, out var identifiers) &&
                    identifiers.All(IsIdentifier),
                "uniqueCollection" => TryGetCollection(factMap, validation.FactId, validation.ValueId, out var uniqueValues) &&
                    uniqueValues.Distinct(StringComparer.OrdinalIgnoreCase).Count() == uniqueValues.Length,
                "disjointCollections" => TryGetCollection(factMap, validation.FactId, validation.ValueId, out var first) &&
                    TryGetCollection(factMap, validation.RelatedFactId, validation.RelatedValueId, out var second) &&
                    !first.Intersect(second, StringComparer.OrdinalIgnoreCase).Any(),
                "arithmeticCollectionReferences" => TryValidateArithmeticCollection(
                    factMap, validation, out _),
                "arithmeticValue" => TryGetValue(
                    factMap, validation.FactId, validation.ValueId, out var arithmeticValue) &&
                    IsArithmeticExpression(arithmeticValue),
                "arithmeticValueReferences" => TryValidateArithmeticValue(
                    factMap, validation, out _),
                "comparisonValueReferences" => TryValidateComparisonValue(factMap, validation),
                "comparisonOperandsReferences" => TryValidateComparisonOperands(factMap, validation),
                "arithmeticExpressionReferences" => TryValidateArithmeticExpressionReferences(factMap, validation),
                "booleanExpressionReferences" => TryValidateBooleanExpressionReferences(factMap, validation),
                "booleanExpressionCollectionReferences" => TryValidateBooleanExpressionCollectionReferences(factMap, validation),
                "valueExpressionReferences" => TryValidateValueExpressionReferences(factMap, validation),
                "valueExpressionCollectionReferences" => TryValidateValueExpressionCollectionReferences(factMap, validation),
                "matchingCollections" => TryValidateMatchingCollections(factMap, validation),
                "orderedArithmeticFunctionExpressions" => TryValidateOrderedArithmeticFunctionExpressions(factMap, validation),
                "matchingLastNumericFunctionCall" => TryValidateLastNumericFunctionCall(factMap, validation),
                "matchingFunctionCall" => TryValidateFunctionCall(factMap, validation, allowStringArguments: true),
                "matchingNumericFunctionCall" => TryValidateFunctionCall(factMap, validation),
                _ => false
            };

            if (!valid)
            {
                error = $"Semantic input validation '{validation.Id}' rejected the extracted values.";
                return false;
            }
        }

        return true;
    }

    private static bool TryValidateArithmeticCollection(
        IReadOnlyDictionary<string, SequenceSemanticFact> facts,
        SequenceInputFactValidation validation,
        out string? ignored)
    {
        ignored = null;
        if (!TryGetCollection(facts, validation.FactId, validation.ValueId, out var expressions) ||
            !TryGetCollection(facts, validation.RelatedFactId, validation.RelatedValueId, out var parameters) ||
            !TryGetCollection(facts, validation.AdditionalFactId, validation.AdditionalValueId, out var localNames) ||
            expressions.Length == 0 ||
            expressions.Length != localNames.Length)
        {
            return false;
        }

        var available = parameters.ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < expressions.Length; index++)
        {
            if (!IsArithmeticExpression(expressions[index]) ||
                !ReferencesOnlyAvailableNames(expressions[index], available))
            {
                return false;
            }

            available.Add(localNames[index]);
        }

        return true;
    }

    private static bool TryValidateArithmeticValue(
        IReadOnlyDictionary<string, SequenceSemanticFact> facts,
        SequenceInputFactValidation validation,
        out string? ignored)
    {
        ignored = null;
        if (!TryGetValue(facts, validation.FactId, validation.ValueId, out var expression) ||
            !TryGetCollection(facts, validation.RelatedFactId, validation.RelatedValueId, out var parameters) ||
            !TryGetCollection(facts, validation.AdditionalFactId, validation.AdditionalValueId, out var localNames) ||
            !IsArithmeticExpression(expression))
        {
            return false;
        }

        var available = parameters.Concat(localNames)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return ReferencesOnlyAvailableNames(expression, available, allowFunctionCalls: true) &&
            ExtractIdentifiers(expression).Any(identifier =>
                localNames.Contains(identifier, StringComparer.OrdinalIgnoreCase));
    }

    private static bool TryValidateComparisonValue(
        IReadOnlyDictionary<string, SequenceSemanticFact> facts,
        SequenceInputFactValidation validation)
    {
        if (!TryGetValue(facts, validation.FactId, validation.ValueId, out var condition) ||
            !TryGetCollection(facts, validation.RelatedFactId, validation.RelatedValueId, out var parameters))
        {
            return false;
        }

        var operators = Regex.Matches(condition, @"==|!=|<=|>=|<|>");
        if (operators.Count != 1)
        {
            return false;
        }

        var comparison = operators[0];
        var left = condition[..comparison.Index].Trim();
        var right = condition[(comparison.Index + comparison.Length)..].Trim();
        if (left.Length == 0 || right.Length == 0 ||
            !IsArithmeticExpression(left) || !IsArithmeticExpression(right))
        {
            return false;
        }

        var availableNames = parameters.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return ReferencesOnlyAvailableNames(left, availableNames) &&
            ReferencesOnlyAvailableNames(right, availableNames);
    }

    private static bool TryValidateComparisonOperands(
        IReadOnlyDictionary<string, SequenceSemanticFact> facts,
        SequenceInputFactValidation validation)
    {
        if (!facts.TryGetValue(validation.FactId, out var condition) ||
            !condition.Values.TryGetValue("left", out var left) ||
            !condition.Values.TryGetValue("operator", out var comparisonOperator) ||
            !condition.Values.TryGetValue("right", out var right) ||
            !TryGetCollection(facts, validation.RelatedFactId, validation.RelatedValueId, out var parameters) ||
            !new[] { "==", "!=", "<", "<=", ">", ">=" }.Contains(comparisonOperator, StringComparer.Ordinal) ||
            !IsArithmeticExpression(left) ||
            !IsArithmeticExpression(right))
        {
            return false;
        }

        var availableNames = parameters.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return ReferencesOnlyAvailableNames(left, availableNames) &&
            ReferencesOnlyAvailableNames(right, availableNames);
    }

    private static bool TryValidateArithmeticExpressionReferences(
        IReadOnlyDictionary<string, SequenceSemanticFact> facts,
        SequenceInputFactValidation validation)
    {
        return TryGetValue(facts, validation.FactId, validation.ValueId, out var expression) &&
            TryGetCollection(facts, validation.RelatedFactId, validation.RelatedValueId, out var parameters) &&
            IsArithmeticExpression(expression) &&
            ReferencesOnlyAvailableNames(expression, parameters.ToHashSet(StringComparer.OrdinalIgnoreCase));
    }


    private static bool TryValidateBooleanExpressionReferences(
        IReadOnlyDictionary<string, SequenceSemanticFact> facts,
        SequenceInputFactValidation validation)
    {
        return TryGetValue(facts, validation.FactId, validation.ValueId, out var expression) &&
            TryGetCollection(facts, validation.RelatedFactId, validation.RelatedValueId, out var parameters) &&
            BooleanExpressionNormalizer.TryValidateReferences(expression, parameters);
    }

    private static bool TryValidateBooleanExpressionCollectionReferences(
        IReadOnlyDictionary<string, SequenceSemanticFact> facts,
        SequenceInputFactValidation validation)
    {
        return TryGetCollection(facts, validation.FactId, validation.ValueId, out var expressions) &&
            TryGetCollection(facts, validation.RelatedFactId, validation.RelatedValueId, out var parameters) &&
            expressions.Length > 0 &&
            expressions.All(expression =>
                BooleanExpressionNormalizer.TryValidateReferences(expression, parameters));
    }

    private static bool TryValidateValueExpressionCollectionReferences(
        IReadOnlyDictionary<string, SequenceSemanticFact> facts,
        SequenceInputFactValidation validation)
    {
        if (!TryGetCollection(facts, validation.FactId, validation.ValueId, out var expressions) ||
            !TryGetCollection(facts, validation.RelatedFactId, validation.RelatedValueId, out var parameters) ||
            expressions.IsDefaultOrEmpty)
        {
            return false;
        }

        var availableNames = parameters.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return expressions.All(expression =>
            IsQuotedStringLiteral(expression) ||
            (IsArithmeticExpression(expression) &&
             ReferencesOnlyAvailableNames(expression, availableNames)));
    }

    private static bool TryValidateOrderedArithmeticFunctionExpressions(
        IReadOnlyDictionary<string, SequenceSemanticFact> facts,
        SequenceInputFactValidation validation)
    {
        if (!TryGetCollection(facts, validation.FactId, validation.ValueId, out var expressions) ||
            !TryGetCollection(facts, validation.RelatedFactId, validation.RelatedValueId, out var parameterLists) ||
            !TryGetCollection(facts, validation.AdditionalFactId, validation.AdditionalValueId, out var functionNames) ||
            expressions.Length == 0 ||
            expressions.Length != parameterLists.Length ||
            expressions.Length != functionNames.Length)
        {
            return false;
        }

        var previousFunctions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < expressions.Length; index++)
        {
            var parameters = parameterLists[index]
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parameters.Length == 0 ||
                parameters.Any(parameter => !IsIdentifier(parameter)) ||
                parameters.Distinct(StringComparer.OrdinalIgnoreCase).Count() != parameters.Length ||
                !IsArithmeticExpression(expressions[index]))
            {
                return false;
            }

            var availableNames = parameters.Concat(previousFunctions)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!ReferencesOnlyAvailableNames(expressions[index], availableNames, allowFunctionCalls: true))
            {
                return false;
            }

            var calledFunctions = Regex.Matches(
                    expressions[index],
                    @"([A-Za-z_][A-Za-z0-9_]*)\\s*\\(")
                .Select(match => match.Groups[1].Value)
                .ToArray();
            if (calledFunctions.Any(name => !previousFunctions.Contains(name)) ||
                (index > 0 && !calledFunctions.Any(previousFunctions.Contains)))
            {
                return false;
            }

            previousFunctions.Add(functionNames[index]);
        }

        return true;
    }

    private static bool TryValidateLastNumericFunctionCall(
        IReadOnlyDictionary<string, SequenceSemanticFact> facts,
        SequenceInputFactValidation validation)
    {
        if (!TryGetValue(facts, validation.FactId, validation.ValueId, out var call) ||
            !TryGetCollection(facts, validation.RelatedFactId, validation.RelatedValueId, out var functionNames) ||
            !TryGetCollection(facts, validation.AdditionalFactId, validation.AdditionalValueId, out var parameterLists))
        {
            return false;
        }

        var match = FunctionCallPattern.Match(call);
        if (!match.Success ||
            !match.Groups["name"].Value.Equals(functionNames[^1], StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var argumentsText = match.Groups["arguments"].Value.Trim();
        var arguments = argumentsText.Length == 0
            ? Array.Empty<string>()
            : argumentsText.Split(',', StringSplitOptions.TrimEntries);
        var parameters = parameterLists[^1]
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return arguments.Length == parameters.Length &&
            arguments.Length > 0 &&
            arguments.All(argument => NumberPattern.IsMatch(argument));
    }

    private static bool TryValidateMatchingCollections(
        IReadOnlyDictionary<string, SequenceSemanticFact> facts,
        SequenceInputFactValidation validation)
    {
        return TryGetCollection(facts, validation.FactId, validation.ValueId, out var first) &&
            TryGetCollection(facts, validation.RelatedFactId, validation.RelatedValueId, out var second) &&
            first.Length > 0 &&
            first.Length == second.Length;
    }

    private static bool TryValidateValueExpressionReferences(
        IReadOnlyDictionary<string, SequenceSemanticFact> facts,
        SequenceInputFactValidation validation)
    {
        if (!TryGetValue(facts, validation.FactId, validation.ValueId, out var expression) ||
            !TryGetCollection(facts, validation.RelatedFactId, validation.RelatedValueId, out var parameters))
        {
            return false;
        }

        return IsQuotedStringLiteral(expression) ||
            (IsArithmeticExpression(expression) &&
             ReferencesOnlyAvailableNames(expression, parameters.ToHashSet(StringComparer.OrdinalIgnoreCase)));
    }

    private static bool TryValidateFunctionCall(
        IReadOnlyDictionary<string, SequenceSemanticFact> facts,
        SequenceInputFactValidation validation,
        bool allowStringArguments = false)
    {
        if (!TryGetValue(facts, validation.FactId, validation.ValueId, out var call) ||
            !TryGetValue(facts, validation.RelatedFactId, validation.RelatedValueId, out var functionName) ||
            !TryGetCollection(facts, validation.AdditionalFactId, validation.AdditionalValueId, out var parameters))
        {
            return false;
        }

        var match = FunctionCallPattern.Match(call);
        if (!match.Success ||
            !match.Groups["name"].Value.Equals(functionName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var argumentsText = match.Groups["arguments"].Value.Trim();
        var arguments = argumentsText.Length == 0
            ? Array.Empty<string>()
            : argumentsText.Split(',', StringSplitOptions.TrimEntries);
        return arguments.Length == parameters.Length &&
            arguments.Length > 0 &&
            arguments.All(argument => NumberPattern.IsMatch(argument) ||
                (allowStringArguments && IsQuotedStringLiteral(argument)));
    }


    internal static bool IsQuotedStringLiteral(string value) =>
        value.Length >= 2 &&
        value[0] is '\'' or '"' &&
        value[^1] == value[0] &&
        !value[1..^1].Contains(value[0]);

    private static bool TryValidateAscendingIntegerRange(
        IReadOnlyDictionary<string, SequenceSemanticFact> facts,
        SequenceInputFactValidation validation)
    {
        return TryGetValue(facts, validation.FactId, validation.ValueId, out var startText) &&
            TryGetValue(facts, validation.RelatedFactId, validation.RelatedValueId, out var endText) &&
            int.TryParse(startText, NumberStyles.None, CultureInfo.InvariantCulture, out var start) &&
            int.TryParse(endText, NumberStyles.None, CultureInfo.InvariantCulture, out var end) &&
            end > start;
    }

    private static bool IsPythonListLiteral(string value)
    {
        if (value.Length < 3 || value[0] != '[' || value[^1] != ']')
        {
            return false;
        }

        var end = value.Length - 1;
        var index = 1;
        var itemCount = 0;

        while (index < end)
        {
            while (index < end && char.IsWhiteSpace(value[index]))
            {
                index++;
            }

            if (index >= end)
            {
                return itemCount > 0;
            }

            if (value[index] is '\'' or '"')
            {
                var quote = value[index++];
                var closed = false;
                while (index < end)
                {
                    var character = value[index++];
                    if (character == '\\')
                    {
                        if (index >= end)
                        {
                            return false;
                        }

                        index++;
                        continue;
                    }

                    if (character == quote)
                    {
                        closed = true;
                        break;
                    }

                    if (character is '\r' or '\n')
                    {
                        return false;
                    }
                }

                if (!closed)
                {
                    return false;
                }
            }
            else
            {
                if (value[index] is '+' or '-')
                {
                    index++;
                }

                var integerStart = index;
                while (index < end && char.IsAsciiDigit(value[index]))
                {
                    index++;
                }

                if (integerStart == index)
                {
                    return false;
                }

                if (index < end && value[index] == '.')
                {
                    index++;
                    var fractionStart = index;
                    while (index < end && char.IsAsciiDigit(value[index]))
                    {
                        index++;
                    }

                    if (fractionStart == index)
                    {
                        return false;
                    }
                }
            }

            itemCount++;
            while (index < end && char.IsWhiteSpace(value[index]))
            {
                index++;
            }

            if (index == end)
            {
                return itemCount > 0;
            }

            if (value[index] != ',')
            {
                return false;
            }

            index++;
        }

        return false;
    }

    internal static bool IsArithmeticExpression(string expression)
    {
        const string tokenPattern = @"[A-Za-z_][A-Za-z0-9_]*|\d+(?:\.\d+)?|[(),+\-*/%]";
        var tokens = Regex.Matches(expression, tokenPattern)
            .Select(match => match.Value)
            .ToArray();
        var remainder = Regex.Replace(expression, tokenPattern, string.Empty);
        if (tokens.Length == 0 || remainder.Any(character => !char.IsWhiteSpace(character)))
        {
            return false;
        }

        var index = 0;
        return TryParseAdditive(tokens, ref index) && index == tokens.Length;
    }

    private static bool TryParseAdditive(string[] tokens, ref int index)
    {
        if (!TryParseMultiplicative(tokens, ref index))
        {
            return false;
        }

        while (index < tokens.Length && tokens[index] is "+" or "-")
        {
            index++;
            if (!TryParseMultiplicative(tokens, ref index))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryParseMultiplicative(string[] tokens, ref int index)
    {
        if (!TryParseUnary(tokens, ref index))
        {
            return false;
        }

        while (index < tokens.Length && tokens[index] is "*" or "/" or "%")
        {
            index++;
            if (!TryParseUnary(tokens, ref index))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryParseUnary(string[] tokens, ref int index)
    {
        if (index < tokens.Length && tokens[index] is "+" or "-")
        {
            index++;
            return TryParseUnary(tokens, ref index);
        }

        return TryParsePrimary(tokens, ref index);
    }

    private static bool TryParsePrimary(string[] tokens, ref int index)
    {
        if (index >= tokens.Length)
        {
            return false;
        }

        if (tokens[index] == "(")
        {
            index++;
            if (!TryParseAdditive(tokens, ref index) ||
                index >= tokens.Length ||
                tokens[index] != ")")
            {
                return false;
            }

            index++;
            return true;
        }

        var token = tokens[index];
        if (IdentifierPattern.IsMatch(token))
        {
            index++;
            if (index >= tokens.Length || tokens[index] != "(")
            {
                return true;
            }

            index++;
            if (!TryParseAdditive(tokens, ref index))
            {
                return false;
            }

            while (index < tokens.Length && tokens[index] == ",")
            {
                index++;
                if (!TryParseAdditive(tokens, ref index))
                {
                    return false;
                }
            }

            if (index >= tokens.Length || tokens[index] != ")")
            {
                return false;
            }

            index++;
            return true;
        }

        if (!NumberPattern.IsMatch(token))
        {
            return false;
        }

        index++;
        return true;
    }

    private static bool ReferencesOnlyAvailableNames(
        string expression,
        HashSet<string> availableNames,
        bool allowFunctionCalls = false)
    {
        var tokens = Regex.Matches(
                expression,
                @"[A-Za-z_][A-Za-z0-9_]*|\d+(?:\.\d+)?|[(),+\-*/%]")
            .Select(match => match.Value)
            .ToArray();

        for (var index = 0; index < tokens.Length; index++)
        {
            if (!IdentifierPattern.IsMatch(tokens[index]))
            {
                continue;
            }

            var isFunctionCall = allowFunctionCalls &&
                index + 1 < tokens.Length &&
                tokens[index + 1] == "(";
            if (!isFunctionCall && !availableNames.Contains(tokens[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<string> ExtractIdentifiers(string expression) =>
        Regex.Matches(expression, @"[A-Za-z_][A-Za-z0-9_]*")
            .Select(match => match.Value);

    private static bool TryGetValue(
        IReadOnlyDictionary<string, SequenceSemanticFact> facts,
        string? factId,
        string? valueId,
        out string value)
    {
        value = string.Empty;
        return factId is not null &&
            valueId is not null &&
            facts.TryGetValue(factId, out var fact) &&
            fact.Values.TryGetValue(valueId, out value!);
    }

    private static bool TryGetCollection(
        IReadOnlyDictionary<string, SequenceSemanticFact> facts,
        string? factId,
        string? valueId,
        out ImmutableArray<string> values)
    {
        values = ImmutableArray<string>.Empty;
        return factId is not null &&
            valueId is not null &&
            facts.TryGetValue(factId, out var fact) &&
            fact.Collections.TryGetValue(valueId, out values) &&
            !values.IsDefaultOrEmpty;
    }

    private static bool IsIdentifier(string value) => IdentifierPattern.IsMatch(value);
}
