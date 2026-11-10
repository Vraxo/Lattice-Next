namespace Lattice.Core.Language;

internal static class EnglishRelationLexicon
{
    private static readonly HashSet<string> BaseRelationVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "like", "love", "watch", "carry", "have", "do", "go",
        "need", "want", "eat", "chase", "admire", "trust"
    };

    private static readonly Dictionary<string, string> PastParticiples = new(StringComparer.OrdinalIgnoreCase)
    {
        ["liked"] = "like",
        ["loved"] = "love",
        ["watched"] = "watch",
        ["carried"] = "carry",
        ["needed"] = "need",
        ["used"] = "use"
    };

    internal static bool IsBaseRelationVerb(string verb) =>
        BaseRelationVerbs.Contains(verb);

    internal static bool TryGetPastParticiple(string participle, out string baseVerb) =>
        PastParticiples.TryGetValue(participle, out baseVerb!);
}
