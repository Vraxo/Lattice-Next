using System.Collections.Immutable;

namespace Lattice.Core.Generation;

internal static class GenerationInputSyntaxParser
{
    public static bool IsIdentifier(string value, GenerationInputSyntax? syntax)
    {
        if (string.IsNullOrEmpty(value) || syntax is null ||
            string.IsNullOrEmpty(syntax.IdentifierStartCharacters) ||
            string.IsNullOrEmpty(syntax.IdentifierContinuationCharacters) ||
            !syntax.IdentifierStartCharacters.Contains(value[0]))
        {
            return false;
        }

        for (var index = 1; index < value.Length; index++)
        {
            if (!syntax.IdentifierContinuationCharacters.Contains(value[index]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsNumberLiteral(string value, GenerationInputSyntax? syntax)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            syntax?.NumberLiteralFormat is not { } format ||
            string.IsNullOrEmpty(format.Digits) ||
            string.IsNullOrEmpty(format.DecimalSeparator) ||
            string.IsNullOrEmpty(format.PositiveSign) ||
            string.IsNullOrEmpty(format.NegativeSign))
        {
            return false;
        }

        var index = 0;
        if (StartsWith(value, index, format.PositiveSign) || StartsWith(value, index, format.NegativeSign))
        {
            index += StartsWith(value, index, format.PositiveSign)
                ? format.PositiveSign.Length
                : format.NegativeSign.Length;
        }

        var integerDigits = ConsumeDigits(value, ref index, format.Digits);
        if (integerDigits == 0)
        {
            return false;
        }

        if (StartsWith(value, index, format.DecimalSeparator))
        {
            index += format.DecimalSeparator.Length;
            if (ConsumeDigits(value, ref index, format.Digits) == 0)
            {
                return false;
            }
        }

        return index == value.Length;
    }

    public static bool IsStringLiteral(string value, GenerationInputSyntax? syntax)
    {
        if (syntax is null || syntax.StringLiteralFormats.IsDefaultOrEmpty)
        {
            return false;
        }

        foreach (var format in syntax.StringLiteralFormats)
        {
            if (TryReadStringLiteralAt(value, 0, format, out var end) && end == value.Length)
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryParseFunctionCall(
        string source,
        GenerationInputSyntax? syntax,
        out string functionName,
        out ImmutableArray<string> arguments)
    {
        functionName = string.Empty;
        arguments = ImmutableArray<string>.Empty;
        if (string.IsNullOrWhiteSpace(source) ||
            syntax is null ||
            syntax.FunctionCallFormat is not { } format)
        {
            return false;
        }

        var index = SkipWhitespace(source, 0);
        if (!TryReadIdentifier(source, index, syntax, out functionName, out index))
        {
            return false;
        }

        index = SkipWhitespace(source, index);
        if (!StartsWith(source, index, format.OpeningDelimiter))
        {
            return false;
        }

        index += format.OpeningDelimiter.Length;
        if (!TryReadArguments(source, index, format, syntax, out arguments, out index))
        {
            return false;
        }

        return SkipWhitespace(source, index) == source.Length;
    }

    private static bool TryReadIdentifier(
        string source,
        int start,
        GenerationInputSyntax syntax,
        out string identifier,
        out int end)
    {
        identifier = string.Empty;
        end = start;
        if (start >= source.Length || !syntax.IdentifierStartCharacters.Contains(source[start]))
        {
            return false;
        }

        end++;
        while (end < source.Length && syntax.IdentifierContinuationCharacters.Contains(source[end]))
        {
            end++;
        }

        identifier = source[start..end];
        return true;
    }

    private static bool TryReadArguments(
        string source,
        int start,
        GenerationFunctionCallFormat format,
        GenerationInputSyntax syntax,
        out ImmutableArray<string> arguments,
        out int end)
    {
        arguments = ImmutableArray<string>.Empty;
        end = start;
        var items = ImmutableArray.CreateBuilder<string>();
        var argumentStart = start;
        var hasSeparator = false;
        var index = start;
        var groupingDelimiters = syntax.GroupingDelimiters.IsDefault
            ? ImmutableArray<GenerationDelimiterPair>.Empty
            : syntax.GroupingDelimiters;
        var delimiters = groupingDelimiters
            .Add(new GenerationDelimiterPair(format.OpeningDelimiter, format.ClosingDelimiter))
            .OrderByDescending(delimiter => delimiter.OpeningDelimiter.Length)
            .ToArray();
        var closingDelimiters = delimiters
            .Select(delimiter => delimiter.ClosingDelimiter)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var expectedClosings = new Stack<string>();

        while (index < source.Length)
        {
            if (TryReadStringAt(source, index, syntax.StringLiteralFormats, out var stringEnd, out var invalidString))
            {
                index = stringEnd;
                continue;
            }

            if (invalidString)
            {
                return false;
            }

            var opening = delimiters.FirstOrDefault(delimiter =>
                StartsWith(source, index, delimiter.OpeningDelimiter));
            if (opening is not null)
            {
                expectedClosings.Push(opening.ClosingDelimiter);
                index += opening.OpeningDelimiter.Length;
                continue;
            }

            if (expectedClosings.TryPeek(out var expectedClosing))
            {
                if (StartsWith(source, index, expectedClosing))
                {
                    expectedClosings.Pop();
                    index += expectedClosing.Length;
                    continue;
                }

                if (closingDelimiters.Any(delimiter => StartsWith(source, index, delimiter)))
                {
                    return false;
                }
            }
            else if (StartsWith(source, index, format.ClosingDelimiter))
            {
                var finalArgument = source[argumentStart..index].Trim();
                if (finalArgument.Length > 0)
                {
                    items.Add(finalArgument);
                }
                else if (hasSeparator)
                {
                    return false;
                }

                arguments = items.ToImmutable();
                end = index + format.ClosingDelimiter.Length;
                return true;
            }
            else if (closingDelimiters.Any(delimiter => StartsWith(source, index, delimiter)))
            {
                return false;
            }

            if (expectedClosings.Count == 0 && StartsWith(source, index, format.ArgumentSeparator))
            {
                var argument = source[argumentStart..index].Trim();
                if (argument.Length == 0)
                {
                    return false;
                }

                items.Add(argument);
                index += format.ArgumentSeparator.Length;
                argumentStart = index;
                hasSeparator = true;
                continue;
            }

            index++;
        }

        return false;
    }

    private static bool TryReadStringAt(
        string source,
        int start,
        ImmutableArray<GenerationInputStringLiteralFormat> formats,
        out int end,
        out bool invalid)
    {
        end = start;
        invalid = false;
        if (formats.IsDefaultOrEmpty)
        {
            return false;
        }

        foreach (var format in formats)
        {
            if (!StartsWith(source, start, format.OpeningDelimiter))
            {
                continue;
            }

            if (!TryReadStringLiteralAt(source, start, format, out end))
            {
                invalid = true;
            }

            return end > start;
        }

        return false;
    }

    private static bool TryReadStringLiteralAt(
        string source,
        int start,
        GenerationInputStringLiteralFormat format,
        out int end)
    {
        end = start;
        if (string.IsNullOrEmpty(format.OpeningDelimiter) ||
            string.IsNullOrEmpty(format.ClosingDelimiter) ||
            string.IsNullOrEmpty(format.EscapePrefix) ||
            format.EscapeSequences.IsDefault ||
            !StartsWith(source, start, format.OpeningDelimiter))
        {
            return false;
        }

        var index = start + format.OpeningDelimiter.Length;
        var escapes = format.EscapeSequences
            .OrderByDescending(sequence => sequence.Length)
            .ToArray();
        while (index < source.Length)
        {
            if (StartsWith(source, index, format.EscapePrefix))
            {
                var escape = escapes.FirstOrDefault(sequence => StartsWith(source, index, sequence));
                if (escape is not null)
                {
                    index += escape.Length;
                    continue;
                }

                if (!format.AllowUnknownEscapes ||
                    index + format.EscapePrefix.Length >= source.Length)
                {
                    return false;
                }

                index += format.EscapePrefix.Length + 1;
                continue;
            }

            var character = source[index];
            if (!format.AllowUnescapedLineBreaks && character is '\r' or '\n')
            {
                return false;
            }

            if (StartsWith(source, index, format.ClosingDelimiter))
            {
                end = index + format.ClosingDelimiter.Length;
                return true;
            }

            index++;
        }

        return false;
    }

    private static int ConsumeDigits(string value, ref int index, string digits)
    {
        var start = index;
        while (index < value.Length && digits.Contains(value[index]))
        {
            index++;
        }

        return index - start;
    }

    private static int SkipWhitespace(string value, int index)
    {
        while (index < value.Length && char.IsWhiteSpace(value[index]))
        {
            index++;
        }

        return index;
    }

    private static bool StartsWith(string value, int index, string prefix) =>
        prefix.Length > 0 &&
        index >= 0 &&
        index + prefix.Length <= value.Length &&
        value.AsSpan(index, prefix.Length).SequenceEqual(prefix.AsSpan());
}
