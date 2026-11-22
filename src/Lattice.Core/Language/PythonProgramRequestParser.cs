using System.Text.RegularExpressions;

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

    private static readonly Regex OutputIntentPattern = new(
        @"\b(?:print|prints|display|displays|show|shows|output|outputs|say|says)\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    internal static bool HasOutputIntent(string source) =>
        OutputIntentPattern.IsMatch(source);

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
        out EnglishSentenceSyntax? syntax)
    {
        syntax = null;
        if (EnglishSyntaxParser.TryParse(source, out syntax) &&
            HasPythonCreationClause(syntax!))
        {
            return true;
        }

        var contextSeparator = source.IndexOf(':');
        return contextSeparator > 0 &&
            EnglishSyntaxParser.TryParse(source[..contextSeparator] + ".", out syntax) &&
            HasPythonCreationClause(syntax!);
    }

    private static bool HasPythonCreationClause(EnglishSentenceSyntax syntax)
    {
        var creationClause = syntax.Clauses.FirstOrDefault(clause =>
            CreationVerbs.Contains(clause.Verb));
        return creationClause is not null &&
            creationClause.Object is { } codeArtifact &&
            ContainsCodeArtifact(codeArtifact) &&
            ContainsPythonLanguage(creationClause);
    }
}
