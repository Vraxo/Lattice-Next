namespace Lattice.Core.Language;

internal static class EnglishRelationLexicon
{
    internal static bool IsBaseRelationVerb(string verb) =>
        EnglishLexicon.BaseRelationVerbs.Contains(verb);

    internal static bool TryGetPastParticiple(string participle, out string baseVerb) =>
        EnglishLexicon.PastParticiples.TryGetValue(participle, out baseVerb!);
}
