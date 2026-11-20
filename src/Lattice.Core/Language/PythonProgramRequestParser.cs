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

    private static readonly HashSet<string> OutputVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "print", "display", "show", "output", "say"
    };

    private static readonly Regex QuotedOutputPattern = new(
        @"\b(?:prints?|displays?|shows?|outputs?|says?)\s+(['""])(?<value>.*?)\1",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);

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

    internal static bool TryGetRequestedOutput(
        EnglishSentenceSyntax syntax,
        int creationClauseIndex,
        string sourceText,
        out string value)
    {
        var outputClause = syntax.Clauses
            .Skip(creationClauseIndex + 1)
            .FirstOrDefault(clause => OutputVerbs.Contains(clause.Verb));

        if (outputClause?.Object is not null &&
            (ContainsHelloWorld(outputClause.Object) ||
             NormalizeOutput(outputClause.Object.Text).Equals("Hello, world!", StringComparison.Ordinal) ||
             QuotedOutputPattern.IsMatch(sourceText)))
        {
            value = NormalizeOutput(outputClause.Object.Text);
            return value.Length > 0;
        }

        var artifact = syntax.Clauses[creationClauseIndex].Object;
        if (artifact is not null && ContainsHelloWorld(artifact))
        {
            value = "Hello, world!";
            return true;
        }

        value = string.Empty;
        return false;
    }

    internal static bool ContainsHelloWorld(EnglishNounPhrase phrase)
    {
        for (var index = 0; index < phrase.Modifiers.Length - 1; index++)
        {
            if (phrase.Modifiers[index].Equals("hello", StringComparison.OrdinalIgnoreCase) &&
                phrase.Modifiers[index + 1].Equals("world", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    internal static string NormalizeOutput(string value)
    {
        var normalized = value.Trim().TrimEnd('.', '?');
        if (normalized.Equals("hello world", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("hello, world", StringComparison.OrdinalIgnoreCase))
        {
            return "Hello, world!";
        }

        return normalized;
    }

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
