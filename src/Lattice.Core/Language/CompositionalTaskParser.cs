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
        "print", "prints", "printing", "display", "displays", "show", "shows",
        "output", "outputs", "say", "says", "write", "writes"
    };

    internal static bool TryParse(
        string source,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        var tokens = EnglishLexer.Tokenize(source);
        var words = tokens
            .Where(token => !token.IsPunctuation && !token.IsQuoted)
            .Select(token => token.Value)
            .ToArray();

        if (!words.Any(CreationVerbs.Contains) ||
            !words.Any(word => word.Equals("python", StringComparison.OrdinalIgnoreCase)) ||
            !ContainsCodeIntent(tokens, words))
        {
            return false;
        }

        var requestedValue = TryGetRequestedOutput(source, tokens, out var output)
            ? output
            : "Hello, world!";

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

    private static bool ContainsCodeIntent(
        ImmutableArray<EnglishToken> tokens,
        string[] words) =>
        words.Any(CodeArtifacts.Contains) ||
        tokens.Any(token =>
            !token.IsQuoted &&
            !token.IsPunctuation &&
            OutputVerbs.Contains(token.Value));

    private static bool TryGetRequestedOutput(
        string source,
        ImmutableArray<EnglishToken> tokens,
        out string value)
    {
        var quotedOutput = tokens.FirstOrDefault(token => token.IsQuoted);
        if (quotedOutput.IsQuoted)
        {
            value = NormalizeOutput(quotedOutput.Value);
            return value.Length > 0;
        }

        if (FindHelloWorld(tokens) >= 0)
        {
            value = "Hello, world!";
            return true;
        }

        for (var index = 0; index < tokens.Length; index++)
        {
            var token = tokens[index];
            if (token.IsQuoted || token.IsPunctuation || !OutputVerbs.Contains(token.Value))
            {
                continue;
            }

            var outputStart = FindNextWord(tokens, index + 1);
            if (outputStart < 0)
            {
                continue;
            }

            var outputEnd = FindOutputEnd(tokens, outputStart);
            if (outputEnd < outputStart)
            {
                continue;
            }

            var firstToken = tokens[outputStart];
            var lastToken = tokens[outputEnd];
            var candidate = source[firstToken.Start..(lastToken.Start + lastToken.Length)].Trim();
            value = NormalizeOutput(RemoveLeadingDeterminer(candidate));
            return value.Length > 0;
        }

        value = string.Empty;
        return false;
    }

    private static int FindHelloWorld(ImmutableArray<EnglishToken> tokens)
    {
        for (var index = 0; index < tokens.Length - 1; index++)
        {
            if (!tokens[index].IsQuoted &&
                !tokens[index + 1].IsQuoted &&
                tokens[index].Value.Equals("hello", StringComparison.OrdinalIgnoreCase) &&
                tokens[index + 1].Value.Equals("world", StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindNextWord(ImmutableArray<EnglishToken> tokens, int start)
    {
        for (var index = start; index < tokens.Length; index++)
        {
            if (!tokens[index].IsPunctuation && !tokens[index].IsQuoted)
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindOutputEnd(ImmutableArray<EnglishToken> tokens, int start)
    {
        var end = start;

        for (var index = start + 1; index < tokens.Length; index++)
        {
            var token = tokens[index];
            if (token.IsPunctuation)
            {
                if (token.Value is "." or "?" or "!")
                {
                    break;
                }

                end = index;
                continue;
            }

            if (!token.IsQuoted &&
                (token.Value.Equals("in", StringComparison.OrdinalIgnoreCase) ||
                 token.Value.Equals("using", StringComparison.OrdinalIgnoreCase)) &&
                index + 1 < tokens.Length &&
                tokens[index + 1].Value.Equals("python", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (!token.IsQuoted &&
                token.Value.Equals("to", StringComparison.OrdinalIgnoreCase) &&
                index + 1 < tokens.Length &&
                (tokens[index + 1].Value.Equals("the", StringComparison.OrdinalIgnoreCase) ||
                 tokens[index + 1].Value.Equals("console", StringComparison.OrdinalIgnoreCase)))
            {
                break;
            }

            end = index;
        }

        return end;
    }

    private static string RemoveLeadingDeterminer(string value)
    {
        foreach (var determiner in new[] { "the ", "a ", "an ", "the text " })
        {
            if (value.StartsWith(determiner, StringComparison.OrdinalIgnoreCase))
            {
                return value[determiner.Length..].Trim();
            }
        }

        return value;
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
