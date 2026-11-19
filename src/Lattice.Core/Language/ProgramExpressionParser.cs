using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class ProgramExpressionParser
{
    internal static bool TryParse(string source, out ProgramExpression? expression)
    {
        expression = null;
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        var parser = new Parser(source);
        if (!parser.TryParseExpression(out expression))
        {
            return false;
        }

        return parser.IsAtEnd;
    }

    private sealed class Parser
    {
        private readonly string source;
        private int position;

        internal Parser(string source)
        {
            this.source = source;
        }

        internal bool IsAtEnd
        {
            get
            {
                SkipWhitespace();
                return position == source.Length;
            }
        }

        internal bool TryParseExpression(out ProgramExpression? expression)
        {
            return TryParseAdditive(out expression);
        }

        private bool TryParseAdditive(out ProgramExpression? expression)
        {
            if (!TryParseMultiplicative(out expression))
            {
                return false;
            }

            while (TryReadOperator('+', out _) || TryReadOperator('-', out _))
            {
                var operation = source[position - 1];
                if (!TryParseMultiplicative(out var right))
                {
                    expression = null;
                    return false;
                }

                expression = new ProgramBinaryExpression(
                    expression!,
                    operation == '+' ? ProgramBinaryOperator.Add : ProgramBinaryOperator.Subtract,
                    right!);
            }

            return true;
        }

        private bool TryParseMultiplicative(out ProgramExpression? expression)
        {
            if (!TryParseUnary(out expression))
            {
                return false;
            }

            while (TryReadOperator('*', out _) ||
                TryReadOperator('/', out _) ||
                TryReadOperator('%', out _))
            {
                var operation = source[position - 1];
                if (!TryParseUnary(out var right))
                {
                    expression = null;
                    return false;
                }

                var binaryOperator = operation switch
                {
                    '*' => ProgramBinaryOperator.Multiply,
                    '/' => ProgramBinaryOperator.Divide,
                    _ => ProgramBinaryOperator.Remainder
                };
                expression = new ProgramBinaryExpression(expression!, binaryOperator, right!);
            }

            return true;
        }

        private bool TryParseUnary(out ProgramExpression? expression)
        {
            SkipWhitespace();
            if (TryReadOperator('-', out _))
            {
                if (!TryParseUnary(out var operand))
                {
                    expression = null;
                    return false;
                }

                expression = new ProgramUnaryExpression(ProgramUnaryOperator.Negate, operand!);
                return true;
            }

            return TryParsePrimary(out expression);
        }

        private bool TryParsePrimary(out ProgramExpression? expression)
        {
            expression = null;
            SkipWhitespace();

            if (position >= source.Length)
            {
                return false;
            }

            if (source[position] == '(')
            {
                position++;
                if (!TryParseExpression(out expression))
                {
                    return false;
                }

                SkipWhitespace();
                if (position >= source.Length || source[position] != ')')
                {
                    expression = null;
                    return false;
                }

                position++;
                return true;
            }

            if (char.IsAsciiDigit(source[position]))
            {
                var start = position;
                while (position < source.Length && char.IsAsciiDigit(source[position]))
                {
                    position++;
                }

                if (position < source.Length && source[position] == '.')
                {
                    position++;
                    var fractionalStart = position;
                    while (position < source.Length && char.IsAsciiDigit(source[position]))
                    {
                        position++;
                    }

                    if (fractionalStart == position)
                    {
                        return false;
                    }
                }

                expression = new ProgramNumberLiteral(source[start..position]);
                return true;
            }

            if (IsIdentifierStart(source[position]))
            {
                var start = position++;
                while (position < source.Length && IsIdentifierPart(source[position]))
                {
                    position++;
                }

                expression = new ProgramNameExpression(source[start..position]);
                return true;
            }

            return false;
        }

        private bool TryReadOperator(char expected, out char operation)
        {
            SkipWhitespace();
            operation = '\0';
            if (position >= source.Length || source[position] != expected)
            {
                return false;
            }

            operation = source[position++];
            return true;
        }

        private void SkipWhitespace()
        {
            while (position < source.Length && char.IsWhiteSpace(source[position]))
            {
                position++;
            }
        }

        private static bool IsIdentifierStart(char value) =>
            char.IsAsciiLetter(value) || value == '_';

        private static bool IsIdentifierPart(char value) =>
            char.IsAsciiLetterOrDigit(value) || value == '_';
    }
}
