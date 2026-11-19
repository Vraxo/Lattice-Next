using System.Text.RegularExpressions;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class CodeRequestParser
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

    internal static bool TryGetCreationClause(
        string source,
        string targetLanguage,
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
            ContainsTargetLanguage(syntax.Clauses[creationClauseIndex], targetLanguage);
    }

    internal static bool TryGetRequestedOutput(
        EnglishSentenceSyntax syntax,
        int creationClauseIndex,
        string sourceText,
        out ProgramSpecification? program)
    {
        program = null;
        if (creationClauseIndex < 0 || creationClauseIndex >= syntax.Clauses.Length)
        {
            return false;
        }

        var outputClause = syntax.Clauses
            .Skip(creationClauseIndex + 1)
            .FirstOrDefault(clause => OutputVerbs.Contains(clause.Verb));

        string value;
        if (outputClause?.Object is not null &&
            (ContainsHelloWorld(outputClause.Object) ||
             NormalizeOutput(outputClause.Object.Text).Equals("Hello, world!", StringComparison.Ordinal) ||
             QuotedOutputPattern.IsMatch(sourceText)))
        {
            value = NormalizeOutput(outputClause.Object.Text);
        }
        else
        {
            var artifact = syntax.Clauses[creationClauseIndex].Object;
            if (artifact is null || !ContainsHelloWorld(artifact))
            {
                return false;
            }

            value = "Hello, world!";
        }

        if (value.Length == 0)
        {
            return false;
        }

        program = new ProgramSpecification(
            [new ProgramOutputStatement(new ProgramStringLiteral(value))]);
        return true;
    }

    private static bool ContainsCodeArtifact(EnglishNounPhrase phrase) =>
        CodeArtifacts.Contains(phrase.Head) ||
        phrase.Modifiers.Any(CodeArtifacts.Contains);

    private static bool ContainsTargetLanguage(EnglishClause clause, string targetLanguage) =>
        ContainsWord(clause.Object, targetLanguage) ||
        clause.PrepositionalPhrases.Any(phrase => ContainsWord(phrase.Object, targetLanguage));

    private static bool ContainsWord(EnglishNounPhrase? phrase, string word) =>
        phrase is not null &&
        (phrase.Head.Equals(word, StringComparison.OrdinalIgnoreCase) ||
         phrase.Modifiers.Any(modifier => modifier.Equals(word, StringComparison.OrdinalIgnoreCase)));

    private static bool ContainsHelloWorld(EnglishNounPhrase phrase)
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

    private static string NormalizeOutput(string value)
    {
        var normalized = value.Trim().TrimEnd('.', '?');
        if (normalized.Equals("hello world", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("hello, world", StringComparison.OrdinalIgnoreCase))
        {
            return "Hello, world!";
        }

        return normalized;
    }
}
