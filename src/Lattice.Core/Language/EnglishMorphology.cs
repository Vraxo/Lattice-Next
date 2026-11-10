namespace Lattice.Core.Language;

internal static class EnglishMorphology
{
    private static readonly Dictionary<string, string> ExplicitPluralNounForms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["algae"] = "alga",
        ["analyses"] = "analysis",
        ["appendices"] = "appendix",
        ["antennae"] = "antenna",
        ["bacteria"] = "bacterium",
        ["bases"] = "basis",
        ["cacti"] = "cactus",
        ["cases"] = "case",
        ["children"] = "child",
        ["courses"] = "course",
        ["crises"] = "crisis",
        ["criteria"] = "criterion",
        ["curricula"] = "curriculum",
        ["diagnoses"] = "diagnosis",
        ["diseases"] = "disease",
        ["emphases"] = "emphasis",
        ["feet"] = "foot",
        ["foci"] = "focus",
        ["formulae"] = "formula",
        ["fungi"] = "fungus",
        ["geese"] = "goose",
        ["horses"] = "horse",
        ["houses"] = "house",
        ["indices"] = "index",
        ["knives"] = "knife",
        ["larvae"] = "larva",
        ["leaves"] = "leaf",
        ["lice"] = "louse",
        ["lives"] = "life",
        ["loci"] = "locus",
        ["matrices"] = "matrix",
        ["men"] = "man",
        ["mice"] = "mouse",
        ["nebulae"] = "nebula",
        ["noises"] = "noise",
        ["noses"] = "nose",
        ["nuclei"] = "nucleus",
        ["nurses"] = "nurse",
        ["oases"] = "oasis",
        ["oxen"] = "ox",
        ["parentheses"] = "parenthesis",
        ["pauses"] = "pause",
        ["people"] = "person",
        ["phenomena"] = "phenomenon",
        ["phases"] = "phase",
        ["phrases"] = "phrase",
        ["promises"] = "promise",
        ["radii"] = "radius",
        ["spouses"] = "spouse",
        ["stimuli"] = "stimulus",
        ["syllabi"] = "syllabus",
        ["teeth"] = "tooth",
        ["theses"] = "thesis",
        ["thieves"] = "thief",
        ["universes"] = "universe",
        ["vertebrae"] = "vertebra",
        ["vertices"] = "vertex",
        ["wives"] = "wife",
        ["women"] = "woman",
        ["wolves"] = "wolf"
    };

    private static readonly HashSet<string> InvariantNouns = new(StringComparer.OrdinalIgnoreCase)
    {
        "deer", "fish", "series", "sheep", "species"
    };

    private static readonly HashSet<string> SingularNounsEndingInS = new(StringComparer.OrdinalIgnoreCase)
    {
        "analysis", "basis", "bonus", "bus", "campus", "crisis", "diagnosis",
        "emphasis", "gas", "hypothesis", "lens", "octopus", "oasis", "parenthesis",
        "cactus", "focus", "fungus", "locus", "nucleus", "radius", "status", "stimulus", "syllabus", "thesis", "virus"
    };

    internal static string InflectThirdPersonSingular(string verb)
    {
        var normalized = verb.ToLowerInvariant();

        if (normalized is "have" or "do" or "go")
        {
            return normalized switch
            {
                "have" => "has",
                "do" => "does",
                "go" => "goes",
                _ => normalized
            };
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

        if (InvariantNouns.Contains(normalized))
        {
            return normalized;
        }

        if (ExplicitPluralNounForms.TryGetValue(normalized, out var irregularSingular))
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
        !InvariantNouns.Contains(word) &&
        !SingularNounsEndingInS.Contains(word) &&
        (ExplicitPluralNounForms.ContainsKey(word) ||
         (word.Length > 1 &&
          word.EndsWith("s", StringComparison.Ordinal) &&
          !word.EndsWith("ss", StringComparison.Ordinal)));

    internal static bool IsPluralHead(string word) =>
        EnglishGrammarTokens.IsLowerWord(word) &&
        (InvariantNouns.Contains(word) || IsPlural(word));

    internal static bool IsThirdPersonVerb(string word) =>
        EnglishGrammarTokens.IsLowerWord(word) &&
        word.Length > 1 &&
        word.EndsWith("s", StringComparison.Ordinal) &&
        !EnglishGrammarTokens.Is(word, "is");

}
