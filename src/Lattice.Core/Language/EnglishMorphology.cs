namespace Lattice.Core.Language;

internal static class EnglishMorphology
{
    internal static string InflectThirdPersonSingular(string verb)
    {
        var normalized = verb.ToLowerInvariant();

        if (EnglishLexicon.IrregularThirdPersonVerbs.TryGetValue(normalized, out var irregular))
        {
            return irregular;
        }

        if (normalized.Length > 1 &&
            normalized.EndsWith("y", StringComparison.Ordinal) &&
            normalized[^2] is not ('a' or 'e' or 'i' or 'o' or 'u'))
        {
            return $"{normalized[..^1]}ies";
        }

        if (normalized.EndsWith("s", StringComparison.Ordinal) ||
            normalized.EndsWith("x", StringComparison.Ordinal) ||
            normalized.EndsWith("z", StringComparison.Ordinal) ||
            normalized.EndsWith("ch", StringComparison.Ordinal) ||
            normalized.EndsWith("sh", StringComparison.Ordinal) ||
            normalized.EndsWith("o", StringComparison.Ordinal))
        {
            return $"{normalized}es";
        }

        return $"{normalized}s";
    }

    internal static string Singularize(string noun)
    {
        var normalized = noun.ToLowerInvariant();

        if (EnglishLexicon.InvariantNouns.Contains(normalized))
        {
            return normalized;
        }

        if (EnglishLexicon.ExplicitPluralNounForms.TryGetValue(normalized, out var irregularSingular))
        {
            return irregularSingular;
        }

        if (normalized.Length > 3 && normalized.EndsWith("ies", StringComparison.Ordinal))
        {
            return $"{normalized[..^3]}y";
        }

        if (normalized.Length > 3 &&
            (normalized.EndsWith("ses", StringComparison.Ordinal) ||
             normalized.EndsWith("xes", StringComparison.Ordinal) ||
             normalized.EndsWith("zes", StringComparison.Ordinal) ||
             normalized.EndsWith("ches", StringComparison.Ordinal) ||
             normalized.EndsWith("shes", StringComparison.Ordinal) ||
             normalized.EndsWith("oes", StringComparison.Ordinal)))
        {
            return normalized[..^2];
        }

        return normalized.Length > 1 && normalized.EndsWith("s", StringComparison.Ordinal)
            ? normalized[..^1]
            : normalized;
    }

    internal static bool IsPlural(string word) =>
        EnglishGrammarTokens.IsLowerWord(word) &&
        !EnglishLexicon.InvariantNouns.Contains(word) &&
        !EnglishLexicon.SingularNounsEndingInS.Contains(word) &&
        (EnglishLexicon.ExplicitPluralNounForms.ContainsKey(word) ||
         (word.Length > 1 &&
          word.EndsWith("s", StringComparison.Ordinal) &&
          !word.EndsWith("ss", StringComparison.Ordinal)));

    internal static bool IsPluralHead(string word) =>
        EnglishGrammarTokens.IsLowerWord(word) &&
        (EnglishLexicon.InvariantNouns.Contains(word) || IsPlural(word));

    internal static bool IsThirdPersonVerb(string word) =>
        EnglishGrammarTokens.IsLowerWord(word) &&
        word.Length > 1 &&
        word.EndsWith("s", StringComparison.Ordinal) &&
        !EnglishGrammarTokens.Is(word, "is");
}
