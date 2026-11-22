using System.Text.RegularExpressions;

namespace Lattice.Core.Generation;

internal static class BooleanExpressionNormalizer
{
    private static readonly Regex ComparisonPattern = new(
        @"^\s*(?<left>[A-Za-z_][A-Za-z0-9_]*)\s+(?<operator>>=|<=|==|!=|>|<|is\s+not\s+equal\s+to|not\s+equal\s+to|is\s+at\s+least|at\s+least|is\s+at\s+most|at\s+most|is\s+greater\s+than|greater\s+than|is\s+less\s+than|less\s+than|is\s+equal\s+to|equal\s+to|equals?)\s+(?<right>.+?)\s*$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    internal static bool TryNormalize(string expression, out string normalized)
    {
        normalized = string.Empty;
        return !string.IsNullOrWhiteSpace(expression) &&
            TryNormalizeExpression(expression, out normalized);
    }

    internal static bool TryValidateReferences(
        string expression,
        IEnumerable<string> availableNames)
    {
        if (!TryNormalizeExpression(expression, out var normalized))
        {
            return false;
        }

        var withoutStrings = Regex.Replace(
            normalized,
            @"'(?:\\.|[^'\\])*'|""(?:\\.|[^""\\])*""",
            string.Empty);
        var available = availableNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Regex.Matches(withoutStrings, @"[A-Za-z_][A-Za-z0-9_]*")
            .Select(match => match.Value)
            .Where(identifier => identifier is not "and" and not "or" and not "not")
            .All(available.Contains);
    }

    private static bool TryNormalizeExpression(string source, out string normalized)
    {
        normalized = string.Empty;
        var expression = source.Trim();
        if (expression.Length == 0)
        {
            return false;
        }

        var isGrouped = false;
        while (HasEnclosingParentheses(expression))
        {
            isGrouped = true;
            expression = expression[1..^1].Trim();
        }

        foreach (var operation in new[] { "or", "and" })
        {
            var operatorIndex = FindTopLevelBooleanOperator(expression, operation);
            if (operatorIndex < 0)
            {
                continue;
            }

            if (!TryNormalizeExpression(expression[..operatorIndex], out var left) ||
                !TryNormalizeExpression(expression[(operatorIndex + operation.Length)..], out var right))
            {
                return false;
            }

            var combined = $"{left} {operation} {right}";
            normalized = isGrouped ? $"({combined})" : combined;
            return true;
        }

        if (expression.StartsWith("not ", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryNormalizeExpression(expression[4..], out var negated))
            {
                return false;
            }

            var combined = $"not {negated}";
            normalized = isGrouped ? $"({combined})" : combined;
            return true;
        }

        var match = ComparisonPattern.Match(expression);
        if (!match.Success)
        {
            return false;
        }

        var leftOperand = match.Groups["left"].Value;
        var rightOperand = match.Groups["right"].Value.Trim();
        if (!SequenceInputFactValidator.IsArithmeticExpression(rightOperand) &&
            !SequenceInputFactValidator.IsQuotedStringLiteral(rightOperand))
        {
            return false;
        }

        var comparison = Regex.Replace(match.Groups["operator"].Value, @"\s+", " ")
            .Trim()
            .ToLowerInvariant();
        var symbol = comparison switch
        {
            ">=" or "<=" or "==" or "!=" or ">" or "<" => comparison,
            "is at least" or "at least" => ">=",
            "is at most" or "at most" => "<=",
            "is greater than" or "greater than" => ">",
            "is less than" or "less than" => "<",
            "is not equal to" or "not equal to" => "!=",
            "is equal to" or "equal to" or "equal" or "equals" => "==",
            _ => string.Empty
        };
        if (symbol.Length == 0)
        {
            return false;
        }

        var normalizedComparison = $"{leftOperand} {symbol} {rightOperand}";
        normalized = isGrouped ? $"({normalizedComparison})" : normalizedComparison;
        return true;
    }

    private static int FindTopLevelBooleanOperator(string expression, string operation)
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
                if (depth < 0)
                {
                    return -1;
                }

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

    private static bool HasEnclosingParentheses(string expression)
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

        return depth == 0 && quote == '\0';
    }
}
