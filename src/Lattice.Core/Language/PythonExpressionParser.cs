using System.Text.RegularExpressions;

namespace Lattice.Core.Language;

internal static class PythonExpressionParser
{
    internal static bool IsArithmeticExpression(string expression)
    {
        var tokenPattern = @"[A-Za-z_][A-Za-z0-9_]*|\d+(?:\.\d+)?|[(),+\-*/%]";
        var tokens = Regex.Matches(expression, tokenPattern)
            .Select(match => match.Value)
            .ToArray();
        var remainder = Regex.Replace(expression, tokenPattern, string.Empty);
        if (tokens.Length == 0 || remainder.Any(character => !char.IsWhiteSpace(character)))
        {
            return false;
        }

        var index = 0;
        return TryParseAdditiveExpression(tokens, ref index) && index == tokens.Length;
    }

    internal static bool TryParseAdditiveExpression(string[] tokens, ref int index)
    {
        if (!TryParseMultiplicativeExpression(tokens, ref index))
        {
            return false;
        }

        while (index < tokens.Length && tokens[index] is "+" or "-")
        {
            index++;
            if (!TryParseMultiplicativeExpression(tokens, ref index))
            {
                return false;
            }
        }

        return true;
    }

    internal static bool TryParseMultiplicativeExpression(string[] tokens, ref int index)
    {
        if (!TryParseUnaryExpression(tokens, ref index))
        {
            return false;
        }

        while (index < tokens.Length && tokens[index] is "*" or "/" or "%")
        {
            index++;
            if (!TryParseUnaryExpression(tokens, ref index))
            {
                return false;
            }
        }

        return true;
    }

    internal static bool TryParseUnaryExpression(string[] tokens, ref int index)
    {
        if (index < tokens.Length && tokens[index] is "+" or "-")
        {
            index++;
            return TryParseUnaryExpression(tokens, ref index);
        }

        return TryParsePrimaryExpression(tokens, ref index);
    }

    internal static bool TryParsePrimaryExpression(string[] tokens, ref int index)
    {
        if (index >= tokens.Length)
        {
            return false;
        }

        if (tokens[index] == "(")
        {
            index++;
            if (!TryParseAdditiveExpression(tokens, ref index) ||
                index >= tokens.Length ||
                tokens[index] != ")")
            {
                return false;
            }

            index++;
            return true;
        }

        var token = tokens[index];
        if (Regex.IsMatch(
            token,
            @"^[A-Za-z_][A-Za-z0-9_]*$",
            RegexOptions.CultureInvariant))
        {
            index++;
            if (index >= tokens.Length || tokens[index] != "(")
            {
                return true;
            }

            index++;
            if (!TryParseAdditiveExpression(tokens, ref index))
            {
                return false;
            }

            while (index < tokens.Length && tokens[index] == ",")
            {
                index++;
                if (!TryParseAdditiveExpression(tokens, ref index))
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

        if (!Regex.IsMatch(
            token,
            @"^\d+(?:\.\d+)?$",
            RegexOptions.CultureInvariant))
        {
            return false;
        }

        index++;
        return true;
    }

    internal static bool TryParseFunctionCondition(
        string condition,
        string[] parameters,
        out string pythonCondition)
    {
        return TryParseBooleanCondition(condition, parameters, out pythonCondition);
    }

    internal static bool TryParseBooleanCondition(
        string condition,
        string[] parameters,
        out string pythonCondition)
    {
        pythonCondition = string.Empty;
        var expression = condition.Trim();
        var isExplicitlyGrouped = false;
        while (HasEnclosingParentheses(expression))
        {
            isExplicitlyGrouped = true;
            expression = expression[1..^1].Trim();
        }

        foreach (var operation in new[] { "or", "and" })
        {
            var operatorIndex = FindTopLevelBooleanOperator(expression, operation);
            if (operatorIndex < 0)
            {
                continue;
            }

            var left = expression[..operatorIndex];
            var right = expression[(operatorIndex + operation.Length)..];
            if (!TryParseBooleanCondition(left, parameters, out var parsedLeft) ||
                !TryParseBooleanCondition(right, parameters, out var parsedRight))
            {
                return false;
            }

            var combined = $"{parsedLeft} {operation} {parsedRight}";
            pythonCondition = isExplicitlyGrouped ? $"({combined})" : combined;
            return true;
        }

        if (expression.StartsWith("not ", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryParseBooleanCondition(expression[4..], parameters, out var negated))
            {
                return false;
            }

            var negatedExpression = $"not {negated}";
            pythonCondition = isExplicitlyGrouped ? $"({negatedExpression})" : negatedExpression;
            return true;
        }

        var match = Regex.Match(
            expression,
            @"^\s*(?<left>[A-Za-z_][A-Za-z0-9_]*)\s+(?<operator>is\s+at\s+least|at\s+least|is\s+at\s+most|at\s+most|is\s+greater\s+than|greater\s+than|is\s+less\s+than|less\s+than|is\s+not\s+equal\s+to|not\s+equal\s+to|is\s+equal\s+to|equals?)\s+(?<right>.+?)\s*$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return false;
        }

        var leftOperand = match.Groups["left"].Value;
        var rightOperand = match.Groups["right"].Value;
        var comparison = Regex.Replace(match.Groups["operator"].Value, @"\s+", " ")
            .Trim()
            .ToLowerInvariant();
        var symbol = comparison switch
        {
            "is at least" or "at least" => ">=",
            "is at most" or "at most" => "<=",
            "is greater than" or "greater than" => ">",
            "is less than" or "less than" => "<",
            "is not equal to" or "not equal to" => "!=",
            "is equal to" or "equal" or "equals" => "==",
            _ => string.Empty
        };

        if (symbol.Length == 0 ||
            !parameters.Contains(leftOperand, StringComparer.OrdinalIgnoreCase) ||
            !TryNormalizeConditionOperand(rightOperand, parameters, out var normalizedRight))
        {
            return false;
        }

        var comparisonExpression = $"{leftOperand} {symbol} {normalizedRight}";
        pythonCondition = isExplicitlyGrouped ? $"({comparisonExpression})" : comparisonExpression;
        return true;
    }

    internal static int FindTopLevelBooleanOperator(string expression, string operation)
    {
        var depth = 0;
        var quote = '\0';
        for (var index = 0; index <= expression.Length - operation.Length; index++)
        {
            var character = expression[index];
            if (quote != '\0')
            {
                if (character == quote && (index == 0 || expression[index - 1] != '\\'))
                {
                    quote = '\0';
                }

                continue;
            }

            if (character is '\'' or '"')
            {
                quote = character;
                continue;
            }

            if (character == '(')
            {
                depth++;
                continue;
            }

            if (character == ')')
            {
                depth--;
                continue;
            }

            if (depth == 0 &&
                string.Compare(expression, index, operation, 0, operation.Length, StringComparison.OrdinalIgnoreCase) == 0 &&
                index > 0 &&
                index + operation.Length < expression.Length &&
                char.IsWhiteSpace(expression[index - 1]) &&
                char.IsWhiteSpace(expression[index + operation.Length]))
            {
                return index;
            }
        }

        return -1;
    }

    internal static bool HasEnclosingParentheses(string expression)
    {
        if (expression.Length < 2 || expression[0] != '(' || expression[^1] != ')')
        {
            return false;
        }

        var depth = 0;
        var quote = '\0';
        for (var index = 0; index < expression.Length; index++)
        {
            var character = expression[index];
            if (quote != '\0')
            {
                if (character == quote && (index == 0 || expression[index - 1] != '\\'))
                {
                    quote = '\0';
                }

                continue;
            }

            if (character is '\'' or '"')
            {
                quote = character;
            }
            else if (character == '(')
            {
                depth++;
            }
            else if (character == ')')
            {
                depth--;
                if (depth == 0 && index != expression.Length - 1)
                {
                    return false;
                }
            }
        }

        return depth == 0;
    }

    internal static bool TryNormalizeConditionOperand(
        string operand,
        string[] parameters,
        out string normalized)
    {
        normalized = operand.Trim();
        if (normalized.Length >= 2 &&
            (normalized[0] == 39 || normalized[0] == 34) &&
            normalized[^1] == normalized[0])
        {
            normalized = ToPythonStringLiteral(normalized[1..^1]);
            return true;
        }

        if (!IsArithmeticExpression(normalized) ||
            Regex.Matches(normalized, @"[A-Za-z_][A-Za-z0-9_]*")
                .Any(identifier => !parameters.Contains(identifier.Value, StringComparer.OrdinalIgnoreCase)))
        {
            return false;
        }

        return true;
    }

    internal static bool TryGetFunctionReturnExpression(
        string value,
        string[] parameters,
        out string expression)
    {
        expression = value.Trim();
        if (expression.Length >= 2 &&
            expression[0] is '\'' or '"' &&
            expression[^1] == expression[0])
        {
            expression = ToPythonStringLiteral(expression[1..^1]);
            return true;
        }

        if (!IsArithmeticExpression(expression))
        {
            return false;
        }

        return Regex.Matches(expression, @"[A-Za-z_][A-Za-z0-9_]*")
            .All(match => parameters.Contains(match.Value, StringComparer.OrdinalIgnoreCase));
    }

    internal static string ToPythonStringLiteral(string value) =>
        "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

}
