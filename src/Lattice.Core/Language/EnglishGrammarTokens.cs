using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class EnglishGrammarTokens
{
    internal static bool IsReservedIndividualTerm(string value) =>
        EnglishLexicon.ReservedTerms.Contains(value);

    internal static bool TryReadSentence(
        string source,
        out string[] tokens,
        out char terminal)
    {
        tokens = [];
        terminal = '\0';

        if (source.Length < 2 || source[^1] is not ('.' or '?'))
        {
            return false;
        }

        terminal = source[^1];
        var body = source[..^1].Trim();

        if (body.Length == 0)
        {
            return false;
        }

        tokens = body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length > 0 && tokens.All(IsWord);
    }

    internal static bool IsArticle(string word) =>
        Is(word, "a") || Is(word, "an");

    internal static bool IsCapitalized(string word) =>
        word.Length > 0 &&
        word[0] is >= 'A' and <= 'Z' &&
        word.Skip(1).All(character =>
            character is >= 'a' and <= 'z' or
                >= 'A' and <= 'Z' or
                >= '0' and <= '9' or '-');

    internal static bool IsLowerWord(string word) =>
        word.Length > 0 &&
        word[0] is >= 'a' and <= 'z' &&
        word.All(character =>
            character is >= 'a' and <= 'z' or
                >= '0' and <= '9' or '-');

    internal static bool IsWord(string word) =>
        word.Length > 0 &&
        char.IsAsciiLetter(word[0]) &&
        word.All(character =>
            char.IsAsciiLetterOrDigit(character) || character == '-');

    internal static bool Is(string actual, string expected) =>
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    internal static SemanticIdentifier CreateIdentifier(string value) =>
        new(value.ToLowerInvariant());
}
