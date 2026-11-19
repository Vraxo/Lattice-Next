using System.Collections.Immutable;
using Lattice.Core.Language;

namespace Lattice.Core.Generation;

public static class SequenceInputFactExtractor
{
    private const int MaximumInputLength = 65_536;

    public static bool TryExtract(
        SequenceKnowledgeBase knowledge,
        string input,
        out ImmutableArray<SequenceSemanticFact> facts,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(knowledge);
        facts = ImmutableArray<SequenceSemanticFact>.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(input) || input.Length > MaximumInputLength)
        {
            error = "A non-empty input within the supported length limit is required.";
            return false;
        }

        if (knowledge.InputRules.IsDefaultOrEmpty)
        {
            error = "The knowledge base contains no semantic input rules.";
            return false;
        }

        var tokens = EnglishLexer.Tokenize(input);
        var extracted = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var matchedAnyRule = false;

        foreach (var rule in knowledge.InputRules)
        {
            if (rule is null ||
                string.IsNullOrWhiteSpace(rule.Id) ||
                string.IsNullOrWhiteSpace(rule.FactId) ||
                rule.MatchTokens.IsDefaultOrEmpty ||
                !TryFindMatch(tokens, rule.MatchTokens, out var lastMatchedToken))
            {
                continue;
            }

            matchedAnyRule = true;
            if (!extracted.TryGetValue(rule.FactId, out var values))
            {
                values = new Dictionary<string, string>(StringComparer.Ordinal);
                extracted.Add(rule.FactId, values);
            }

            if (rule.ValueId is null)
            {
                continue;
            }

            if (!TryCaptureValue(input, tokens, lastMatchedToken, out var capturedValue))
            {
                continue;
            }

            if (values.TryGetValue(rule.ValueId, out var existingValue))
            {
                if (!string.Equals(existingValue, capturedValue, StringComparison.Ordinal))
                {
                    error = $"Input rules produced conflicting values for semantic fact '{rule.FactId}'.";
                    return false;
                }

                continue;
            }

            values.Add(rule.ValueId, capturedValue);
        }

        if (!matchedAnyRule)
        {
            error = "No semantic input rule matched the supplied text.";
            return false;
        }

        facts = extracted
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => new SequenceSemanticFact(
                item.Key,
                item.Value.ToImmutableDictionary(StringComparer.Ordinal)))
            .ToImmutableArray();
        return true;
    }

    private static bool TryFindMatch(
        ImmutableArray<EnglishToken> tokens,
        ImmutableArray<string> pattern,
        out int lastMatchedToken)
    {
        lastMatchedToken = -1;
        var patternIndex = 0;

        for (var tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
        {
            var token = tokens[tokenIndex];
            if (token.IsQuoted || token.IsPunctuation)
            {
                continue;
            }

            if (!token.Value.Equals(pattern[patternIndex], StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            lastMatchedToken = tokenIndex;
            patternIndex++;
            if (patternIndex == pattern.Length)
            {
                return true;
            }
        }

        lastMatchedToken = -1;
        return false;
    }

    private static bool TryCaptureValue(
        string input,
        ImmutableArray<EnglishToken> tokens,
        int lastMatchedToken,
        out string value)
    {
        value = string.Empty;
        if (lastMatchedToken < 0 || lastMatchedToken >= tokens.Length)
        {
            return false;
        }

        var quotedValue = tokens
            .Skip(lastMatchedToken + 1)
            .FirstOrDefault(token => token.IsQuoted);
        if (quotedValue.IsQuoted)
        {
            value = quotedValue.Value;
            return value.Length > 0;
        }

        var captureStart = tokens[lastMatchedToken].Start +
            tokens[lastMatchedToken].Length;
        if (captureStart >= input.Length)
        {
            return false;
        }

        value = input[captureStart..].Trim();
        if (value.EndsWith(".", StringComparison.Ordinal) ||
            value.EndsWith("?", StringComparison.Ordinal))
        {
            value = value[..^1].TrimEnd();
        }

        return value.Length > 0;
    }
}
