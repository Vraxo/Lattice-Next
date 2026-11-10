namespace Lattice.Core.Language;

internal static class EnglishRelationLexicon
{
    private static readonly HashSet<string> BaseRelationVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "admire", "build", "buy", "carry", "catch", "chase", "close", "create",
        "do", "eat", "follow", "give", "go", "hear", "have", "help", "hold",
        "know", "keep", "leave", "like", "love", "make", "need", "open", "read",
        "see", "sell", "send", "take", "teach", "tell", "trust", "use", "visit",
        "want", "watch", "write"
    };

    private static readonly Dictionary<string, string> PastParticiples = new(StringComparer.OrdinalIgnoreCase)
    {
        ["admired"] = "admire",
        ["bought"] = "buy",
        ["built"] = "build",
        ["carried"] = "carry",
        ["caught"] = "catch",
        ["chased"] = "chase",
        ["closed"] = "close",
        ["created"] = "create",
        ["eaten"] = "eat",
        ["followed"] = "follow",
        ["found"] = "find",
        ["given"] = "give",
        ["heard"] = "hear",
        ["held"] = "hold",
        ["helped"] = "help",
        ["kept"] = "keep",
        ["known"] = "know",
        ["left"] = "leave",
        ["liked"] = "like",
        ["loved"] = "love",
        ["made"] = "make",
        ["needed"] = "need",
        ["opened"] = "open",
        ["read"] = "read",
        ["seen"] = "see",
        ["sold"] = "sell",
        ["sent"] = "send",
        ["taken"] = "take",
        ["taught"] = "teach",
        ["told"] = "tell",
        ["trusted"] = "trust",
        ["used"] = "use",
        ["visited"] = "visit",
        ["watched"] = "watch",
        ["wanted"] = "want",
        ["written"] = "write"
    };

    internal static bool IsBaseRelationVerb(string verb) =>
        BaseRelationVerbs.Contains(verb);

    internal static bool TryGetPastParticiple(string participle, out string baseVerb) =>
        PastParticiples.TryGetValue(participle, out baseVerb!);
}
