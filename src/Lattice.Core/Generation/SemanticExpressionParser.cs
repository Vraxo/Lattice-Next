using System.Collections.Immutable;

namespace Lattice.Core.Generation;

internal static class SemanticExpressionParser
{
    private const int MaximumSourceLength = 16_384;
    private const int MaximumTokens = 2048;

    private enum TokenKind
    {
        Identifier,
        Number,
        Text,
        Operator,
        OpenDelimiter,
        CloseDelimiter,
        ArgumentSeparator,
        End
    }

    private sealed record Token(
        TokenKind Kind,
        string Value,
        string? MatchingDelimiter = null);

    private sealed class Parser(
        ImmutableArray<Token> tokens,
        GenerationInputSyntax syntax)
    {
        private int position;

        public bool TryParse(out SemanticExpression? expression)
        {
            expression = null;
            return TryParseExpression(0, out expression) &&
                Current.Kind == TokenKind.End;
        }

        private Token Current => tokens[position];

        private bool TryParseExpression(int minimumPrecedence, out SemanticExpression? expression)
        {
            expression = null;
            if (!TryParsePrefixOrPrimary(out var left))
            {
                return false;
            }

            while (Current.Kind == TokenKind.Operator &&
                   TryGetOperator(Current, "infix", out var format) &&
                   format.Precedence >= minimumPrecedence)
            {
                position++;
                if (!TryParseExpression(format.Precedence + 1, out var right) ||
                    !TryMapOperatorKind(format.Kind, out var expressionOperator))
                {
                    return false;
                }

                left = new SemanticBinaryExpression(expressionOperator, left!, right!);
            }

            expression = left;
            return true;
        }

        private bool TryParsePrefixOrPrimary(out SemanticExpression? expression)
        {
            expression = null;
            if (Current.Kind == TokenKind.Operator && TryGetOperator(Current, "prefix", out var prefix))
            {
                position++;
                if (!TryMapOperatorKind(prefix.Kind, out var prefixOperator) ||
                    !TryParseExpression(prefix.Precedence, out var operand))
                {
                    return false;
                }

                expression = new SemanticUnaryExpression(prefixOperator, operand!);
                return true;
            }

            if (Current.Kind == TokenKind.OpenDelimiter)
            {
                var closingDelimiter = Current.MatchingDelimiter;
                position++;
                if (closingDelimiter is null ||
                    !TryParseExpression(0, out var grouped) ||
                    Current.Kind != TokenKind.CloseDelimiter ||
                    Current.Value != closingDelimiter)
                {
                    return false;
                }

                position++;
                expression = grouped;
                return true;
            }

            if (Current.Kind == TokenKind.Number)
            {
                if (!GenerationInputSyntaxParser.TryNormalizeNumberLiteral(
                        Current.Value, syntax.NumberLiteralFormat, out var canonicalNumber))
                {
                    return false;
                }

                expression = new SemanticNumberExpression(canonicalNumber);
                position++;
                return true;
            }

            if (Current.Kind == TokenKind.Text)
            {
                expression = new SemanticTextExpression(Current.Value);
                position++;
                return true;
            }

            if (Current.Kind != TokenKind.Identifier)
            {
                return false;
            }

            var name = Current.Value;
            position++;
            if (Current.Kind != TokenKind.OpenDelimiter ||
                Current.Value != syntax.FunctionCallFormat.OpeningDelimiter)
            {
                expression = new SemanticNameExpression(name);
                return true;
            }

            position++;
            var arguments = ImmutableArray.CreateBuilder<SemanticExpression>();
            if (Current.Kind == TokenKind.CloseDelimiter &&
                Current.Value == syntax.FunctionCallFormat.ClosingDelimiter)
            {
                position++;
                expression = new SemanticFunctionCallExpression(name, arguments.ToImmutable());
                return true;
            }

            while (true)
            {
                if (!TryParseExpression(0, out var argument))
                {
                    return false;
                }

                arguments.Add(argument!);
                if (Current.Kind == TokenKind.ArgumentSeparator)
                {
                    position++;
                    continue;
                }

                if (Current.Kind != TokenKind.CloseDelimiter ||
                    Current.Value != syntax.FunctionCallFormat.ClosingDelimiter)
                {
                    return false;
                }

                position++;
                expression = new SemanticFunctionCallExpression(name, arguments.ToImmutable());
                return true;
            }
        }

        private bool TryGetOperator(
            Token token,
            string fixity,
            out GenerationInputExpressionOperatorFormat format)
        {
            format = syntax.ExpressionOperators.FirstOrDefault(candidate =>
                candidate.Token == token.Value && candidate.Fixity == fixity)!;
            return format is not null;
        }
    }

    public static bool TryParse(
        string kind,
        string source,
        GenerationInputSyntax? syntax,
        out SemanticExpression? expression)
    {
        expression = null;
        if (syntax is null)
        {
            return false;
        }

        switch (kind)
        {
            case "arithmetic":
                return TryParseArithmetic(source, syntax, out expression);
            case "boolean":
                return BooleanExpressionNormalizer.TryParse(source, syntax, out expression);
            case "value":
                return TryParseValue(source, syntax, out expression);
            case "functionCall":
                return TryParseSyntax(source, syntax, out expression) &&
                    expression is SemanticFunctionCallExpression;
            default:
                return false;
        }
    }

    public static bool TryParseArithmetic(
        string source,
        GenerationInputSyntax syntax,
        out SemanticExpression? expression) =>
        TryParseSyntax(source, syntax, out expression) &&
        SemanticExpressionAnalysis.IsArithmetic(expression!);

    public static bool TryParseValue(
        string source,
        GenerationInputSyntax syntax,
        out SemanticExpression? expression) =>
        TryParseSyntax(source, syntax, out expression) &&
        SemanticExpressionAnalysis.IsValue(expression!);

    public static bool TryParseBooleanSyntax(
        string source,
        GenerationInputSyntax syntax,
        out SemanticExpression? expression) =>
        TryParseSyntax(source, syntax, out expression) &&
        SemanticExpressionAnalysis.IsBoolean(expression!);

    public static bool TryParseComparisonOperator(
        string source,
        GenerationInputSyntax syntax,
        out SemanticExpressionOperator expressionOperator)
    {
        expressionOperator = default;
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        var matchingToken = syntax.ExpressionOperators.FirstOrDefault(format =>
            format.Fixity == "infix" &&
            format.Token.Equals(source.Trim(), StringComparison.Ordinal));
        if (matchingToken is not null &&
            TryMapOperatorKind(matchingToken.Kind, out expressionOperator) &&
            IsComparisonOperator(expressionOperator))
        {
            return true;
        }

        return ComparisonOperatorNormalizer.TryParse(source, out expressionOperator);
    }

    public static bool TryParseSyntax(
        string source,
        GenerationInputSyntax syntax,
        out SemanticExpression? expression)
    {
        expression = null;
        if (string.IsNullOrWhiteSpace(source) || source.Length > MaximumSourceLength ||
            syntax.ExpressionOperators.IsDefaultOrEmpty ||
            !TryTokenize(source, syntax, out var tokens))
        {
            return false;
        }

        var parser = new Parser(tokens, syntax);
        return parser.TryParse(out expression) &&
            SemanticExpressionAnalysis.IsValid(expression);
    }

    public static string GetOperatorKind(SemanticExpressionOperator expressionOperator) => expressionOperator switch
    {
        SemanticExpressionOperator.Add => "add",
        SemanticExpressionOperator.Subtract => "subtract",
        SemanticExpressionOperator.Multiply => "multiply",
        SemanticExpressionOperator.Divide => "divide",
        SemanticExpressionOperator.Remainder => "remainder",
        SemanticExpressionOperator.Equal => "equal",
        SemanticExpressionOperator.NotEqual => "notEqual",
        SemanticExpressionOperator.LessThan => "lessThan",
        SemanticExpressionOperator.LessThanOrEqual => "lessThanOrEqual",
        SemanticExpressionOperator.GreaterThan => "greaterThan",
        SemanticExpressionOperator.GreaterThanOrEqual => "greaterThanOrEqual",
        SemanticExpressionOperator.And => "and",
        SemanticExpressionOperator.Or => "or",
        SemanticExpressionOperator.Not => "not",
        SemanticExpressionOperator.Positive => "positive",
        SemanticExpressionOperator.Negative => "negative",
        _ => string.Empty
    };

    private static bool IsComparisonOperator(SemanticExpressionOperator expressionOperator) =>
        expressionOperator is SemanticExpressionOperator.Equal or
            SemanticExpressionOperator.NotEqual or
            SemanticExpressionOperator.LessThan or
            SemanticExpressionOperator.LessThanOrEqual or
            SemanticExpressionOperator.GreaterThan or
            SemanticExpressionOperator.GreaterThanOrEqual;

    private static bool TryMapOperatorKind(string kind, out SemanticExpressionOperator expressionOperator)
    {
        expressionOperator = kind switch
        {
            "add" => SemanticExpressionOperator.Add,
            "subtract" => SemanticExpressionOperator.Subtract,
            "multiply" => SemanticExpressionOperator.Multiply,
            "divide" => SemanticExpressionOperator.Divide,
            "remainder" => SemanticExpressionOperator.Remainder,
            "equal" => SemanticExpressionOperator.Equal,
            "notEqual" => SemanticExpressionOperator.NotEqual,
            "lessThan" => SemanticExpressionOperator.LessThan,
            "lessThanOrEqual" => SemanticExpressionOperator.LessThanOrEqual,
            "greaterThan" => SemanticExpressionOperator.GreaterThan,
            "greaterThanOrEqual" => SemanticExpressionOperator.GreaterThanOrEqual,
            "and" => SemanticExpressionOperator.And,
            "or" => SemanticExpressionOperator.Or,
            "not" => SemanticExpressionOperator.Not,
            "positive" => SemanticExpressionOperator.Positive,
            "negative" => SemanticExpressionOperator.Negative,
            _ => (SemanticExpressionOperator)(-1)
        };

        return Enum.IsDefined(expressionOperator);
    }

    private static bool TryTokenize(
        string source,
        GenerationInputSyntax syntax,
        out ImmutableArray<Token> tokens)
    {
        tokens = ImmutableArray<Token>.Empty;
        if (syntax.ExpressionOperators.IsDefaultOrEmpty ||
            syntax.StringLiteralFormats.IsDefaultOrEmpty ||
            syntax.FunctionCallFormat is null)
        {
            return false;
        }

        var delimiters = GenerationInputSyntaxParser.GetDelimiters(syntax).ToArray();
        var operators = syntax.ExpressionOperators
            .OrderByDescending(format => format.Token.Length)
            .ToArray();
        var builder = ImmutableArray.CreateBuilder<Token>();
        var index = 0;

        while (index < source.Length)
        {
            if (char.IsWhiteSpace(source[index]))
            {
                index++;
                continue;
            }

            if (GenerationInputSyntaxParser.TryReadStringLiteral(
                    source, index, syntax, out var text, out var stringEnd))
            {
                builder.Add(new Token(TokenKind.Text, text));
                index = stringEnd;
                continue;
            }

            if (GenerationInputSyntaxParser.TryReadUnsignedNumberAt(
                    source, index, syntax.NumberLiteralFormat, out var number, out var numberEnd))
            {
                builder.Add(new Token(TokenKind.Number, number));
                index = numberEnd;
                continue;
            }

            var wordOperator = operators.FirstOrDefault(format =>
                IsWordOperator(format.Token) &&
                StartsWith(source, index, format.Token) &&
                IsWordBoundary(source, index, format.Token.Length, syntax));
            if (wordOperator is not null)
            {
                builder.Add(new Token(TokenKind.Operator, wordOperator.Token));
                index += wordOperator.Token.Length;
                continue;
            }

            if (GenerationInputSyntaxParser.TryReadIdentifier(
                    source, index, syntax, out var identifier, out var identifierEnd))
            {
                builder.Add(new Token(TokenKind.Identifier, identifier));
                index = identifierEnd;
                continue;
            }

            var openingDelimiter = delimiters.FirstOrDefault(delimiter =>
                StartsWith(source, index, delimiter.OpeningDelimiter));
            if (openingDelimiter is not null)
            {
                builder.Add(new Token(
                    TokenKind.OpenDelimiter,
                    openingDelimiter.OpeningDelimiter,
                    MatchingDelimiter: openingDelimiter.ClosingDelimiter));
                index += openingDelimiter.OpeningDelimiter.Length;
                continue;
            }

            var closingDelimiter = delimiters.FirstOrDefault(delimiter =>
                StartsWith(source, index, delimiter.ClosingDelimiter));
            if (closingDelimiter is not null)
            {
                builder.Add(new Token(TokenKind.CloseDelimiter, closingDelimiter.ClosingDelimiter));
                index += closingDelimiter.ClosingDelimiter.Length;
                continue;
            }

            if (StartsWith(source, index, syntax.FunctionCallFormat.ArgumentSeparator))
            {
                builder.Add(new Token(TokenKind.ArgumentSeparator, syntax.FunctionCallFormat.ArgumentSeparator));
                index += syntax.FunctionCallFormat.ArgumentSeparator.Length;
                continue;
            }

            var symbolicOperator = operators.FirstOrDefault(format =>
                !IsWordOperator(format.Token) && StartsWith(source, index, format.Token));
            if (symbolicOperator is not null)
            {
                builder.Add(new Token(TokenKind.Operator, symbolicOperator.Token));
                index += symbolicOperator.Token.Length;
                continue;
            }

            return false;
        }

        if (builder.Count == 0 || builder.Count > MaximumTokens)
        {
            return false;
        }

        builder.Add(new Token(TokenKind.End, string.Empty));
        tokens = builder.ToImmutable();
        return true;
    }

    private static bool IsWordOperator(string token) =>
        token.Length > 0 && token.All(char.IsLetter);

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
