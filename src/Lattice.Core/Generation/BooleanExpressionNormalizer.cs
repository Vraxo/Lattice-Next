using System.Text.RegularExpressions;

namespace Lattice.Core.Generation;

internal static class BooleanExpressionNormalizer
{
    private static readonly Regex ComparisonPattern = new(
        @"^\s*(?<left>.+?)\s+(?<operator>is\s+not\s+equal\s+to|not\s+equal\s+to|is\s+greater\s+than\s+or\s+equal\s+to|greater\s+than\s+or\s+equal\s+to|is\s+less\s+than\s+or\s+equal\s+to|less\s+than\s+or\s+equal\s+to|is\s+at\s+least|at\s+least|is\s+at\s+most|at\s+most|is\s+greater\s+than|greater\s+than|is\s+less\s+than|less\s+than|more\s+than|below|is\s+equal\s+to|equal\s+to|equal|equals?)\s+(?<right>.+?)\s*$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    public static bool TryParse(
        string source,
        GenerationInputSyntax syntax,
        out SemanticExpression? expression)
    {
        expression = null;
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        var value = source.Trim();
        if (SemanticExpressionParser.TryParseBooleanSyntax(value, syntax, out expression))
        {
            return true;
        }

        while (TryStripEnclosingDelimiters(value, syntax, out var inner))
        {
            value = inner.Trim();
        }

        var comparison = ComparisonPattern.Match(value);
        if (comparison.Success &&
            SemanticExpressionParser.TryParseComparisonOperator(
                comparison.Groups["operator"].Value, syntax, out var comparisonOperator) &&
            SemanticExpressionParser.TryParseValue(comparison.Groups["left"].Value.Trim(), syntax, out var leftOperand) &&
            SemanticExpressionParser.TryParseValue(comparison.Groups["right"].Value.Trim(), syntax, out var rightOperand))
        {
            expression = new SemanticBinaryExpression(comparisonOperator, leftOperand!, rightOperand!);
            return true;
        }

        foreach (var operation in new[] { "or", "and" })
        {
            var operatorIndex = FindTopLevelBooleanOperator(value, operation, syntax);
            if (operatorIndex < 0)
            {
                continue;
            }

            var rightStart = operatorIndex + operation.Length;
            if (!TryParse(value[..operatorIndex], syntax, out var left) ||
                !TryParse(value[rightStart..], syntax, out var right))
            {
                return false;
            }

            expression = new SemanticBinaryExpression(
                operation == "or" ? SemanticExpressionOperator.Or : SemanticExpressionOperator.And,
                left!,
                right!);
            return true;
        }

        if (TryReadLeadingWord(value, "not", syntax, out var negatedText))
        {
            if (!TryParse(negatedText, syntax, out var negated))
            {
                return false;
            }

            expression = new SemanticUnaryExpression(SemanticExpressionOperator.Not, negated!);
            return true;
        }

        return SemanticExpressionParser.TryParseBooleanSyntax(value, syntax, out expression);
    }

    private static int FindTopLevelBooleanOperator(
        string expression,
        string operation,
        GenerationInputSyntax syntax)
    {
        var delimiters = GenerationInputSyntaxParser.GetDelimiters(syntax).ToArray();
        var expectedClosings = new Stack<string>();
        var index = 0;

        while (index < expression.Length)
        {
            if (GenerationInputSyntaxParser.TryReadStringLiteral(
                    expression, index, syntax, out _, out var stringEnd))
            {
                index = stringEnd;
                continue;
            }

            var opening = delimiters.FirstOrDefault(delimiter =>
                StartsWith(expression, index, delimiter.OpeningDelimiter));
            if (opening is not null)
            {
                expectedClosings.Push(opening.ClosingDelimiter);
                index += opening.OpeningDelimiter.Length;
                continue;
            }

            if (expectedClosings.TryPeek(out var expectedClosing))
            {
                if (StartsWith(expression, index, expectedClosing))
                {
                    expectedClosings.Pop();
                    index += expectedClosing.Length;
                    continue;
                }

                if (delimiters.Any(delimiter => StartsWith(expression, index, delimiter.ClosingDelimiter)))
                {
                    return -1;
                }
            }
            else if (delimiters.Any(delimiter => StartsWith(expression, index, delimiter.ClosingDelimiter)))
            {
                return -1;
            }

            if (expectedClosings.Count == 0 &&
                StartsWith(expression, index, operation) &&
                IsWordBoundary(expression, index, operation.Length, syntax))
            {
                if (operation == "or" && IsComparisonPhraseOr(expression, index))
                {
                    index += operation.Length;
                    continue;
                }

                return index;
            }

            index++;
        }

        return expectedClosings.Count == 0 ? -1 : -1;
    }

    private static bool IsComparisonPhraseOr(string expression, int operatorIndex)
    {
        var left = expression[..operatorIndex].TrimEnd();
        var right = expression[(operatorIndex + "or".Length)..].TrimStart();
        var leftIsOrderedComparison =
            left.EndsWith("greater than", StringComparison.OrdinalIgnoreCase) ||
            left.EndsWith("less than", StringComparison.OrdinalIgnoreCase);
        return leftIsOrderedComparison &&
            right.StartsWith("equal to", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryStripEnclosingDelimiters(
        string expression,
        GenerationInputSyntax syntax,
        out string inner)
    {
        inner = string.Empty;
        if (expression.Length < 2)
        {
            return false;
        }

        var allDelimiters = GenerationInputSyntaxParser.GetDelimiters(syntax);
        var delimiters = allDelimiters
            .Where(delimiter => StartsWith(expression, 0, delimiter.OpeningDelimiter));

        foreach (var delimiter in delimiters)
        {
            if (!expression.EndsWith(delimiter.ClosingDelimiter, StringComparison.Ordinal))
            {
                continue;
            }

            var expectedClosings = new Stack<string>();
            var index = 0;
            while (index < expression.Length)
            {
                if (GenerationInputSyntaxParser.TryReadStringLiteral(
                        expression, index, syntax, out _, out var stringEnd))
                {
                    index = stringEnd;
                    continue;
                }

                var opening = allDelimiters.FirstOrDefault(candidate =>
                    StartsWith(expression, index, candidate.OpeningDelimiter));
                if (opening is not null)
                {
                    expectedClosings.Push(opening.ClosingDelimiter);
                    index += opening.OpeningDelimiter.Length;
                    continue;
                }

                if (expectedClosings.TryPeek(out var expectedClosing) &&
                    StartsWith(expression, index, expectedClosing))
                {
                    expectedClosings.Pop();
                    index += expectedClosing.Length;
                    if (expectedClosings.Count == 0)
                    {
                        if (index != expression.Length)
                        {
                            break;
                        }

                        inner = expression[delimiter.OpeningDelimiter.Length..^delimiter.ClosingDelimiter.Length];
                        return true;
                    }

                    continue;
                }

                if (allDelimiters.Any(candidate =>
                        StartsWith(expression, index, candidate.ClosingDelimiter)))
                {
                    break;
                }

                index++;
            }
        }

        return false;
    }

    private static bool TryReadLeadingWord(
        string expression,
        string word,
        GenerationInputSyntax syntax,
        out string remainder)
    {
        remainder = string.Empty;
        if (!StartsWith(expression, 0, word) ||
            !IsWordBoundary(expression, 0, word.Length, syntax))
        {
            return false;
        }

        var index = word.Length;
        while (index < expression.Length && char.IsWhiteSpace(expression[index]))
        {
            index++;
        }

        if (index == word.Length || index == expression.Length)
        {
            return false;
        }

        remainder = expression[index..];
        return true;
    }

    private static bool IsWordBoundary(
        string source,
        int index,
        int tokenLength,
        GenerationInputSyntax syntax)
    {
        var beforeIsIdentifier = index > 0 && syntax.IdentifierContinuationCharacters.Contains(source[index - 1]);
        var afterIndex = index + tokenLength;
        var afterIsIdentifier = afterIndex < source.Length &&
            syntax.IdentifierContinuationCharacters.Contains(source[afterIndex]);
        return !beforeIsIdentifier && !afterIsIdentifier;
    }

    private static bool StartsWith(string value, int index, string prefix) =>
        prefix.Length > 0 &&
        index >= 0 &&
        index + prefix.Length <= value.Length &&
        value.AsSpan(index, prefix.Length).SequenceEqual(prefix.AsSpan());
}
