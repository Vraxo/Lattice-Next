namespace Lattice.Core.Language;

internal static class PythonProgramRequestParser
{
    private static readonly HashSet<string> CreationVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "write", "create", "generate", "make", "build", "define"
    };

    private static readonly HashSet<string> CodeArtifacts = new(StringComparer.OrdinalIgnoreCase)
    {
        "program", "script", "application", "code", "function", "class"
    };

    internal static bool ContainsCodeArtifact(EnglishNounPhrase phrase) =>
        CodeArtifacts.Contains(phrase.Head) ||
        phrase.Modifiers.Any(CodeArtifacts.Contains);

    internal static bool ContainsPythonLanguage(EnglishClause clause) =>
        ContainsWord(clause.Object, "python") ||
        clause.PrepositionalPhrases.Any(phrase => ContainsWord(phrase.Object, "python"));

    internal static bool ContainsWord(EnglishNounPhrase? phrase, string word) =>
        phrase is not null &&
        (phrase.Head.Equals(word, StringComparison.OrdinalIgnoreCase) ||
         phrase.Modifiers.Any(modifier => modifier.Equals(word, StringComparison.OrdinalIgnoreCase)));

    internal static bool TryGetCreationClause(
        string source,
        out EnglishSentenceSyntax? syntax,
        out int creationClauseIndex)
    {
        syntax = null;
        creationClauseIndex = -1;
        if (!EnglishSyntaxParser.TryParse(source, out syntax))
        {
            return false;
        }

        for (var index = 0; index < syntax!.Clauses.Length; index++)
        {
            if (CreationVerbs.Contains(syntax.Clauses[index].Verb))
            {
                creationClauseIndex = index;
                break;
            }
        }

        return creationClauseIndex >= 0 &&
            syntax.Clauses[creationClauseIndex].Object is { } codeArtifact &&
            ContainsCodeArtifact(codeArtifact) &&
            ContainsPythonLanguage(syntax.Clauses[creationClauseIndex]);
    }
}
