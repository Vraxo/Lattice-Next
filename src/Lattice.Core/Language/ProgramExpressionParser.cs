using System.Collections.Immutable;
using System.Text;
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


    internal static bool TryParseListLiteral(string source, out ProgramListExpression? expression)
    {
        expression = null;
        var normalized = source.Trim();
        if (normalized.Length < 2 || normalized[0] != '[' || normalized[^1] != ']')
        {
            return false;
        }

        var parser = new Parser(normalized[1..^1]);
        if (!parser.TryParseListItems(out var items))
        {
            return false;
        }

        expression = new ProgramListExpression(items);
        return true;
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


        internal bool TryParseListItems(out ImmutableArray<ProgramExpression> items)
        {
            var parsed = ImmutableArray.CreateBuilder<ProgramExpression>();
            SkipWhitespace();
            if (position == source.Length)
            {
                items = parsed.ToImmutable();
                return true;
            }

            while (true)
            {
                if (!TryParseExpression(out var item))
                {
                    items = ImmutableArray<ProgramExpression>.Empty;
                    return false;
                }

                parsed.Add(item!);
                SkipWhitespace();
                if (position == source.Length)
                {
                    items = parsed.ToImmutable();
                    return true;
                }

                if (source[position] != ',')
                {
                    items = ImmutableArray<ProgramExpression>.Empty;
                    return false;
                }

                position++;
                SkipWhitespace();
                if (position == source.Length)
                {
                    items = ImmutableArray<ProgramExpression>.Empty;
                    return false;
                }
            }
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


            if (source[position] is '\'' or '"')
            {
                var quote = source[position++];
                var builder = new StringBuilder();
                var closed = false;
                while (position < source.Length)
                {
                    var character = source[position++];
                    if (character == quote)
                    {
                        closed = true;
                        break;
                    }

                    if (character == '\\')
                    {
                        if (position >= source.Length)
                        {
                            return false;
                        }

                        character = source[position++] switch
                        {
                            'n' => '\n',
                            'r' => '\r',
                            't' => '\t',
                            '\\' => '\\',
                            '\'' => '\'',
                            '"' => '"',
                            var escaped => escaped
                        };
                    }

                    builder.Append(character);
                }

                if (!closed)
                {
                    return false;
                }

                expression = new ProgramStringLiteral(builder.ToString());
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
