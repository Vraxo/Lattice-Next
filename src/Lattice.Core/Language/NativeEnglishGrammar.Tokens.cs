using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static partial class NativeEnglishGrammar
{
    private static bool IsArticle(string word) =>
        Is(word, "a") || Is(word, "an");

    private static bool IsCapitalized(string word) =>
        word.Length > 0 &&
        word[0] is >= 'A' and <= 'Z' &&
        word.Skip(1).All(character =>
            character is >= 'a' and <= 'z' or
                >= 'A' and <= 'Z' or
                >= '0' and <= '9' or '-');

    private static bool IsLowerWord(string word) =>
        word.Length > 0 &&
        word[0] is >= 'a' and <= 'z' &&
        word.All(character =>
            character is >= 'a' and <= 'z' or
                >= '0' and <= '9' or '-');

    private static bool IsWord(string word) =>
        word.Length > 0 &&
        char.IsAsciiLetter(word[0]) &&
        word.All(character =>
            char.IsAsciiLetterOrDigit(character) || character == '-');

    private static bool Is(string actual, string expected) =>
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    private static SemanticIdentifier Id(string value) =>
        new(value.ToLowerInvariant());
}
