using System.Collections.Immutable;
using System.Text;

namespace Lattice.Core.Language;

internal static class EnglishLexer
{
    internal static ImmutableArray<EnglishToken> Tokenize(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var tokens = ImmutableArray.CreateBuilder<EnglishToken>();
        var index = 0;

        while (index < source.Length)
        {
            if (char.IsWhiteSpace(source[index]))
            {
                index++;
                continue;
            }

            var start = index;
            var character = source[index];

            if (IsQuote(character))
            {
                var closingQuote = GetClosingQuote(character);
                index++;
                var valueStart = index;

                while (index < source.Length && source[index] != closingQuote)
                {
                    if (source[index] == '\\' && index + 1 < source.Length)
                    {
                        index += 2;
                        continue;
                    }

                    index++;
                }

                var value = UnescapeQuotedValue(source[valueStart..index]);
                if (index < source.Length)
                {
                    index++;
                }

                tokens.Add(new EnglishToken(value, true, false, start, index - start));
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                index++;

                while (index < source.Length)
                {
                    var current = source[index];
                    var isInternalApostrophe = current == '\'' &&
                        index + 1 < source.Length &&
                        char.IsLetter(source[index + 1]);

                    if (char.IsLetterOrDigit(current) ||
                        current is '-' or '\u2011' ||
                        isInternalApostrophe)
                    {
                        index++;
                        continue;
                    }

                    break;
                }

                tokens.Add(new EnglishToken(
                    source[start..index],
                    false,
                    false,
                    start,
                    index - start));
                continue;
            }

            tokens.Add(new EnglishToken(
                character.ToString(),
                false,
                true,
                start,
                1));
            index++;
        }

        return tokens.ToImmutable();
    }

    private static bool IsQuote(char value) =>
        value is '\'' or '"' or '\u2018' or '\u201C';

    private static char GetClosingQuote(char value) =>
        value switch
        {
            '\u2018' => '\u2019',
            '\u201C' => '\u201D',
            _ => value
        };

    private static string UnescapeQuotedValue(string value)
    {
        var result = new StringBuilder(value.Length);

        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] == '\\' && index + 1 < value.Length &&
                (value[index + 1] == '\\' || value[index + 1] == '\'' || value[index + 1] == '"'))
            {
                index++;
            }

            result.Append(value[index]);
        }

        return result.ToString();
    }
}
