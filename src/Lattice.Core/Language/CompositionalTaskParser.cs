using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class CompositionalTaskParser
{
    private static readonly HashSet<string> CreationVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "write", "create", "generate", "make", "build"
    };

    private static readonly HashSet<string> CodeArtifacts = new(StringComparer.OrdinalIgnoreCase)
    {
        "program", "script", "application", "code"
    };

    private static readonly HashSet<string> OutputVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "print", "display", "show", "output", "say"
    };

    internal static bool TryParse(
        string source,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        if (!EnglishSyntaxParser.TryParse(source, out var syntax))
        {
            return false;
        }

        var mainClause = syntax!.Clauses[0];
        if (!CreationVerbs.Contains(mainClause.Verb) ||
            mainClause.Object is null ||
            !ContainsCodeArtifact(mainClause.Object) ||
            !syntax.Tokens.Any(token =>
                !token.IsQuoted &&
                token.Value.Equals("python", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (!TryGetRequestedOutput(syntax, out var requestedValue))
        {
            return false;
        }

        var formula = new TaskRequestFormula(
            TaskRequestAction.Code,
            "python-program",
            "python",
            "python-source",
            "write-standard-output",
            ImmutableArray.Create("text-output"))
        {
            RequestedValue = requestedValue
        };

        utterance = new SemanticUtterance(
            UtteranceKind.TaskRequest,
            formula,
            sourceText.Trim());
        return true;
    }

    private static bool ContainsCodeArtifact(EnglishNounPhrase phrase) =>
        CodeArtifacts.Contains(phrase.Head) ||
        phrase.Modifiers.Any(CodeArtifacts.Contains);

    private static bool TryGetRequestedOutput(
        EnglishSentenceSyntax syntax,
        out string value)
    {
        var outputClause = syntax.Clauses
            .Skip(1)
            .FirstOrDefault(clause => OutputVerbs.Contains(clause.Verb));

        if (outputClause?.Object is not null)
        {
            value = NormalizeOutput(outputClause.Object.Text);
            return value.Length > 0;
        }

        for (var index = 0; index < syntax.Tokens.Length - 1; index++)
        {
            if (!syntax.Tokens[index].IsQuoted &&
                !syntax.Tokens[index + 1].IsQuoted &&
                syntax.Tokens[index].Value.Equals("hello", StringComparison.OrdinalIgnoreCase) &&
                syntax.Tokens[index + 1].Value.Equals("world", StringComparison.OrdinalIgnoreCase))
            {
                value = "Hello, world!";
                return true;
            }
        }

        value = string.Empty;
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
