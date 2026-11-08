using System.Text;

namespace Lattice.Core.Language;

public static class InputNormalizer
{
    public static string Normalize(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        string source;

        try
        {
            source = input.Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            source = input;
        }

        var result = new StringBuilder(source.Length);

        for (var index = 0; index < source.Length; index++)
        {
            var character = source[index];

            if (character == '\uFEFF')
            {
                if (index > 0)
                {
                    result.Append(' ');
                }

                continue;
            }

            if (character == '\u00AD')
            {
                continue;
            }

            if (character == '\u200B' || char.IsWhiteSpace(character))
            {
                result.Append(' ');
                continue;
            }

            result.Append(character);
        }

        return result.ToString();
    }

    internal static bool IsTextSeparator(char character) =>
        char.IsWhiteSpace(character) ||
        character is '\u200B' or '\uFEFF';
}
