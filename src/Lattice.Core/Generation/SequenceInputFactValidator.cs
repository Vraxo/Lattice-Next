using System.Collections.Immutable;
using System.Globalization;

namespace Lattice.Core.Generation;

public static class SequenceInputFactValidator
{
    public static bool TryValidate(
        ImmutableArray<SequenceInputFactValidation> validations,
        ImmutableArray<SequenceSemanticFact> facts,
        out string? error) =>
        TryValidate(validations, facts, languagePack: null, out error);

    public static bool TryValidate(
        ImmutableArray<SequenceInputFactValidation> validations,
        ImmutableArray<SequenceSemanticFact> facts,
        GenerationLanguagePack? languagePack,
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

            var inputSyntax = languagePack?.InputSyntax;
            var valid = validation.Kind switch
            {
                "identifierValue" => TryGetValue(
                        factMap, validation.FactId, validation.ValueId, out var value) &&
                    GenerationInputSyntaxParser.IsIdentifier(value, inputSyntax),
                "integerValue" => TryGetValue(
                        factMap, validation.FactId, validation.ValueId, out var integerValue) &&
                    int.TryParse(integerValue, NumberStyles.None, CultureInfo.InvariantCulture, out _),
                "ascendingIntegerRange" => TryValidateAscendingIntegerRange(factMap, validation),
                "semanticCollection" => TryGetSemanticCollection(
                        factMap, validation.FactId, validation.ValueId, out var semanticCollection) &&
                    SemanticCollectionValidator.IsValid(semanticCollection),
                "identifierCollection" => TryGetCollection(
                        factMap, validation.FactId, validation.ValueId, out var identifiers) &&
                    identifiers.All(identifier =>
                        GenerationInputSyntaxParser.IsIdentifier(identifier, inputSyntax)),
                "uniqueCollection" => TryGetCollection(
                        factMap, validation.FactId, validation.ValueId, out var uniqueValues) &&
                    uniqueValues.Distinct(StringComparer.OrdinalIgnoreCase).Count() == uniqueValues.Length,
                "disjointCollections" => TryGetCollection(
                        factMap, validation.FactId, validation.ValueId, out var first) &&
                    TryGetCollection(factMap, validation.RelatedFactId, validation.RelatedValueId, out var second) &&
                    !first.Intersect(second, StringComparer.OrdinalIgnoreCase).Any(),
                "arithmeticCollectionReferences" => TryValidateArithmeticCollection(factMap, validation),
                "arithmeticValue" => TryGetExpression(
                        factMap, validation.FactId, validation.ValueId, out var arithmeticValue) &&
                    SemanticExpressionAnalysis.IsArithmetic(arithmeticValue),
                "arithmeticValueReferences" => TryValidateArithmeticValue(factMap, validation),
                "comparisonValueReferences" => TryValidateComparisonValue(factMap, validation),
                "arithmeticExpressionReferences" => TryValidateArithmeticExpressionReferences(factMap, validation),
                "booleanExpressionCollectionReferences" => TryValidateBooleanExpressionCollectionReferences(factMap, validation),
                "valueExpressionReferences" => TryValidateValueExpressionReferences(factMap, validation),
                "valueExpressionCollectionReferences" => TryValidateValueExpressionCollectionReferences(factMap, validation),
                "matchingCollections" => TryValidateMatchingCollections(factMap, validation),
                "orderedArithmeticFunctionExpressions" => TryValidateOrderedArithmeticFunctionExpressions(
                    factMap, validation, inputSyntax),
                "matchingLastNumericFunctionCall" => TryValidateLastNumericFunctionCall(
                    factMap, validation, inputSyntax),
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
        SequenceInputFactValidation validation)
    {
        if (!TryGetExpressionCollection(facts, validation.FactId, validation.ValueId, out var expressions) ||
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
            if (!SemanticExpressionAnalysis.IsArithmetic(expressions[index]) ||
                !SemanticExpressionAnalysis.ReferencesOnlyAvailableNames(expressions[index], available))
            {
                return false;
            }

            available.Add(localNames[index]);
        }

        return true;
    }

    private static bool TryValidateArithmeticValue(
        IReadOnlyDictionary<string, SequenceSemanticFact> facts,
        SequenceInputFactValidation validation)
    {
        if (!TryGetExpression(facts, validation.FactId, validation.ValueId, out var expression) ||
            !TryGetCollection(facts, validation.RelatedFactId, validation.RelatedValueId, out var parameters) ||
            !TryGetCollection(facts, validation.AdditionalFactId, validation.AdditionalValueId, out var localNames) ||
            !SemanticExpressionAnalysis.IsArithmetic(expression))
        {
            return false;
        }

        var available = parameters.Concat(localNames)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return SemanticExpressionAnalysis.ReferencesOnlyAvailableNames(
                expression, available, allowFunctionCalls: true) &&
            SemanticExpressionAnalysis.ReferencesAnyName(expression, localNames);
    }

    private static bool TryValidateComparisonValue(
        IReadOnlyDictionary<string, SequenceSemanticFact> facts,
        SequenceInputFactValidation validation)
    {
        return TryGetExpression(facts, validation.FactId, validation.ValueId, out var expression) &&
            IsComparison(expression) &&
            TryGetCollection(facts, validation.RelatedFactId, validation.RelatedValueId, out var parameters) &&
            SemanticExpressionAnalysis.ReferencesOnlyAvailableNames(
                expression, parameters.ToHashSet(StringComparer.OrdinalIgnoreCase));
    }

    private static bool TryValidateArithmeticExpressionReferences(
        IReadOnlyDictionary<string, SequenceSemanticFact> facts,
        SequenceInputFactValidation validation)
    {
        return TryGetExpression(facts, validation.FactId, validation.ValueId, out var expression) &&
            TryGetCollection(facts, validation.RelatedFactId, validation.RelatedValueId, out var parameters) &&
            SemanticExpressionAnalysis.IsArithmetic(expression) &&
            SemanticExpressionAnalysis.ReferencesOnlyAvailableNames(
                expression, parameters.ToHashSet(StringComparer.OrdinalIgnoreCase));
    }

    private static bool TryValidateBooleanExpressionCollectionReferences(
        IReadOnlyDictionary<string, SequenceSemanticFact> facts,
        SequenceInputFactValidation validation)
    {
        return TryGetExpressionCollection(facts, validation.FactId, validation.ValueId, out var expressions) &&
            TryGetCollection(facts, validation.RelatedFactId, validation.RelatedValueId, out var parameters) &&
            expressions.Length > 0 &&
            expressions.All(expression =>
                SemanticExpressionAnalysis.IsBoolean(expression) &&
                SemanticExpressionAnalysis.ReferencesOnlyAvailableNames(
                    expression, parameters.ToHashSet(StringComparer.OrdinalIgnoreCase)));
    }

    private static bool TryValidateValueExpressionCollectionReferences(
        IReadOnlyDictionary<string, SequenceSemanticFact> facts,
        SequenceInputFactValidation validation)
    {
        return TryGetExpressionCollection(facts, validation.FactId, validation.ValueId, out var expressions) &&
            TryGetCollection(facts, validation.RelatedFactId, validation.RelatedValueId, out var parameters) &&
            expressions.Length > 0 &&
            expressions.All(expression =>
                SemanticExpressionAnalysis.IsValue(expression) &&
                SemanticExpressionAnalysis.ReferencesOnlyAvailableNames(
                    expression, parameters.ToHashSet(StringComparer.OrdinalIgnoreCase)));
    }

    private static bool TryValidateOrderedArithmeticFunctionExpressions(
        IReadOnlyDictionary<string, SequenceSemanticFact> facts,
        SequenceInputFactValidation validation,
        GenerationInputSyntax? inputSyntax)
    {
        if (inputSyntax?.FunctionCallFormat is not { } functionCallFormat ||
            !TryGetExpressionCollection(facts, validation.FactId, validation.ValueId, out var expressions) ||
            !TryGetCollection(facts, validation.RelatedFactId, validation.RelatedValueId, out var parameterLists) ||
            !TryGetCollection(facts, validation.AdditionalFactId, validation.AdditionalValueId, out var functionNames) ||
            expressions.Length == 0 ||
            expressions.Length != parameterLists.Length ||
            expressions.Length != functionNames.Length)
        {
            return false;
        }

        var previousFunctions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var definedFunctions = functionNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < expressions.Length; index++)
        {
            var parameters = parameterLists[index]
                .Split(functionCallFormat.ArgumentSeparator,
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parameters.Length == 0 ||
                parameters.Any(parameter =>
                    !GenerationInputSyntaxParser.IsIdentifier(parameter, inputSyntax)) ||
                parameters.Distinct(StringComparer.OrdinalIgnoreCase).Count() != parameters.Length ||
                !SemanticExpressionAnalysis.IsArithmetic(expressions[index]))
            {
                return false;
            }

            var availableNames = parameters.Concat(previousFunctions)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var calledFunctions = SemanticExpressionAnalysis.GetFunctionNames(expressions[index]);
            if (!SemanticExpressionAnalysis.ReferencesOnlyAvailableNames(
                    expressions[index],
                    availableNames,
                    allowFunctionCalls: true) ||
                calledFunctions.Any(functionName =>
                    definedFunctions.Contains(functionName) && !previousFunctions.Contains(functionName)) ||
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
        SequenceInputFactValidation validation,
        GenerationInputSyntax? inputSyntax)
    {
        if (!TryGetExpression(facts, validation.FactId, validation.ValueId, out var expression) ||
            expression is not SemanticFunctionCallExpression call ||
            !TryGetCollection(facts, validation.RelatedFactId, validation.RelatedValueId, out var functionNames) ||
            !TryGetCollection(facts, validation.AdditionalFactId, validation.AdditionalValueId, out var parameterLists) ||
            inputSyntax?.FunctionCallFormat is not { } functionCallFormat)
        {
            return false;
        }

        var parameters = parameterLists[^1]
            .Split(functionCallFormat.ArgumentSeparator,
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return call.Name.Equals(functionNames[^1], StringComparison.OrdinalIgnoreCase) &&
            call.Arguments.Length == parameters.Length &&
            call.Arguments.Length > 0 &&
            call.Arguments.All(IsNumericLiteralExpression);
    }

    private static bool TryValidateMatchingCollections(
        IReadOnlyDictionary<string, SequenceSemanticFact> facts,
        SequenceInputFactValidation validation)
    {
        return TryGetCollectionLength(facts, validation.FactId, validation.ValueId, out var firstLength) &&
            TryGetCollectionLength(facts, validation.RelatedFactId, validation.RelatedValueId, out var secondLength) &&
            firstLength > 0 && firstLength == secondLength;
    }

    private static bool TryValidateValueExpressionReferences(
        IReadOnlyDictionary<string, SequenceSemanticFact> facts,
        SequenceInputFactValidation validation)
    {
        return TryGetExpression(facts, validation.FactId, validation.ValueId, out var expression) &&
            TryGetCollection(facts, validation.RelatedFactId, validation.RelatedValueId, out var parameters) &&
            SemanticExpressionAnalysis.IsValue(expression) &&
            SemanticExpressionAnalysis.ReferencesOnlyAvailableNames(
                expression, parameters.ToHashSet(StringComparer.OrdinalIgnoreCase));
    }

    private static bool TryValidateFunctionCall(
        IReadOnlyDictionary<string, SequenceSemanticFact> facts,
        SequenceInputFactValidation validation,
        bool allowStringArguments = false)
    {
        if (!TryGetExpression(facts, validation.FactId, validation.ValueId, out var expression) ||
            expression is not SemanticFunctionCallExpression call ||
            !TryGetValue(facts, validation.RelatedFactId, validation.RelatedValueId, out var functionName) ||
            !TryGetCollection(facts, validation.AdditionalFactId, validation.AdditionalValueId, out var parameters))
        {
            return false;
        }

        return call.Name.Equals(functionName, StringComparison.OrdinalIgnoreCase) &&
            call.Arguments.Length == parameters.Length &&
            call.Arguments.Length > 0 &&
            call.Arguments.All(argument => IsNumericLiteralExpression(argument) ||
                (allowStringArguments && argument is SemanticTextExpression));
    }

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

    private static bool IsNumericLiteralExpression(SemanticExpression expression) =>
        expression is SemanticNumberExpression ||
        expression is SemanticUnaryExpression
        {
            Operator: SemanticExpressionOperator.Positive or SemanticExpressionOperator.Negative,
            Operand: SemanticNumberExpression
        };

    private static bool IsComparison(SemanticExpression expression) =>
        expression is SemanticBinaryExpression binary &&
        IsComparisonOperator(binary.Operator) &&
        SemanticExpressionAnalysis.IsValue(binary.Left) &&
        SemanticExpressionAnalysis.IsValue(binary.Right);

    private static bool IsComparisonOperator(SemanticExpressionOperator expressionOperator) =>
        expressionOperator is SemanticExpressionOperator.Equal or
            SemanticExpressionOperator.NotEqual or
            SemanticExpressionOperator.LessThan or
            SemanticExpressionOperator.LessThanOrEqual or
            SemanticExpressionOperator.GreaterThan or
            SemanticExpressionOperator.GreaterThanOrEqual;

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

    private static bool TryGetExpression(
        IReadOnlyDictionary<string, SequenceSemanticFact> facts,
        string? factId,
        string? valueId,
        out SemanticExpression expression)
    {
        expression = null!;
        return factId is not null &&
            valueId is not null &&
            facts.TryGetValue(factId, out var fact) &&
            fact.Expressions.TryGetValue(valueId, out expression!) &&
            SemanticExpressionAnalysis.IsValid(expression);
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

    private static bool TryGetExpressionCollection(
        IReadOnlyDictionary<string, SequenceSemanticFact> facts,
        string? factId,
        string? valueId,
        out ImmutableArray<SemanticExpression> expressions)
    {
        expressions = ImmutableArray<SemanticExpression>.Empty;
        return factId is not null &&
            valueId is not null &&
            facts.TryGetValue(factId, out var fact) &&
            fact.ExpressionCollections.TryGetValue(valueId, out expressions) &&
            !expressions.IsDefaultOrEmpty &&
            expressions.All(expression => SemanticExpressionAnalysis.IsValid(expression));
    }

    private static bool TryGetCollectionLength(
        IReadOnlyDictionary<string, SequenceSemanticFact> facts,
        string? factId,
        string? valueId,
        out int length)
    {
        length = 0;
        if (factId is null || valueId is null || !facts.TryGetValue(factId, out var fact))
        {
            return false;
        }

        if (fact.Collections.TryGetValue(valueId, out var values))
        {
            length = values.Length;
            return !values.IsDefault;
        }

        if (fact.ExpressionCollections.TryGetValue(valueId, out var expressions))
        {
            length = expressions.Length;
            return !expressions.IsDefault;
        }

        return false;
    }

    private static bool TryGetSemanticCollection(
        IReadOnlyDictionary<string, SequenceSemanticFact> facts,
        string factId,
        string valueId,
        out SemanticCollection collection)
    {
        collection = null!;
        return facts.TryGetValue(factId, out var fact) &&
            fact.SemanticCollections is not null &&
            fact.SemanticCollections.TryGetValue(valueId, out collection!);
    }
}
