using System.Collections.Immutable;
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
                "identifierCollection" => TryGetCollection(factMap, validation.FactId, validation.ValueId, out var identifiers) &&
                    identifiers.All(IsIdentifier),
                "uniqueCollection" => TryGetCollection(factMap, validation.FactId, validation.ValueId, out var uniqueValues) &&
                    uniqueValues.Distinct(StringComparer.OrdinalIgnoreCase).Count() == uniqueValues.Length,
                "disjointCollections" => TryGetCollection(factMap, validation.FactId, validation.ValueId, out var first) &&
                    TryGetCollection(factMap, validation.RelatedFactId, validation.RelatedValueId, out var second) &&
                    !first.Intersect(second, StringComparer.OrdinalIgnoreCase).Any(),
                "arithmeticCollectionReferences" => TryValidateArithmeticCollection(
                    factMap, validation, out _),
                "arithmeticValueReferences" => TryValidateArithmeticValue(
                    factMap, validation, out _),
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

    private static bool TryValidateFunctionCall(
        IReadOnlyDictionary<string, SequenceSemanticFact> facts,
        SequenceInputFactValidation validation)
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
            arguments.All(argument => NumberPattern.IsMatch(argument));
    }

    private static bool IsArithmeticExpression(string expression)
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
