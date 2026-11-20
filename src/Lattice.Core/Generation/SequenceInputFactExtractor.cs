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
        var extracted = new Dictionary<string, Dictionary<string, (string Value, int Specificity)>>(StringComparer.Ordinal);
        var matchedAnyRule = false;

        foreach (var rule in knowledge.InputRules)
        {
            if (rule is null ||
                string.IsNullOrWhiteSpace(rule.Id) ||
                string.IsNullOrWhiteSpace(rule.FactId) ||
                rule.MatchTokens.IsDefaultOrEmpty ||
                !TryFindMatch(tokens, rule.MatchTokens, 0, out var lastMatchedToken))
            {
                continue;
            }

            matchedAnyRule = true;
            if (!extracted.ContainsKey(rule.FactId))
            {
                extracted.Add(
                    rule.FactId,
                    new Dictionary<string, (string Value, int Specificity)>(StringComparer.Ordinal));
            }

            if (rule.ValueId is not null)
            {
                var capturedValue = rule.LiteralValue;
                if (capturedValue is null &&
                    !TryCaptureValue(
                        input,
                        tokens,
                        lastMatchedToken,
                        ImmutableArray<string>.Empty,
                        ImmutableArray<string>.Empty,
                        out capturedValue))
                {
                    capturedValue = null;
                }

                if (capturedValue is not null &&
                    !TryAddValue(extracted, rule.FactId, rule.ValueId, capturedValue, rule.MatchTokens.Length, out error))
                {
                    return false;
                }
            }

            if (rule.Captures.IsDefaultOrEmpty)
            {
                continue;
            }

            foreach (var capture in rule.Captures)
            {
                var capturedValue = capture.LiteralValue;
                if (capturedValue is null &&
                    !TryCaptureValue(
                        input,
                        tokens,
                        lastMatchedToken,
                        capture.StartAfterTokens,
                        capture.StopBeforeTokens,
                        out capturedValue))
                {
                    continue;
                }

                if (!TryAddValue(
                    extracted,
                    capture.FactId,
                    capture.ValueId,
                    capturedValue,
                    rule.MatchTokens.Length + capture.StartAfterTokens.Length,
                    out error))
                {
                    return false;
                }
            }
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
                item.Value.ToImmutableDictionary(
                    value => value.Key,
                    value => value.Value.Value,
                    StringComparer.Ordinal)))
            .ToImmutableArray();
        return true;
    }

    private static bool TryAddValue(
        Dictionary<string, Dictionary<string, (string Value, int Specificity)>> extracted,
        string factId,
        string valueId,
        string value,
        int specificity,
        out string? error)
    {
        error = null;
        if (!extracted.TryGetValue(factId, out var values))
        {
            values = new Dictionary<string, (string Value, int Specificity)>(StringComparer.Ordinal);
            extracted.Add(factId, values);
        }

        if (!values.TryGetValue(valueId, out var existing))
        {
            values.Add(valueId, (value, specificity));
            return true;
        }

        if (specificity > existing.Specificity)
        {
            values[valueId] = (value, specificity);
            return true;
        }

        if (specificity == existing.Specificity &&
            !string.Equals(existing.Value, value, StringComparison.Ordinal))
        {
            error = $"Input rules produced conflicting values for semantic fact '{factId}'.";
            return false;
        }

        return true;
    }

    private static bool TryFindMatch(
        ImmutableArray<EnglishToken> tokens,
        ImmutableArray<string> pattern,
        int startIndex,
        out int lastMatchedToken)
    {
        lastMatchedToken = -1;
        var patternIndex = 0;

        for (var tokenIndex = startIndex; tokenIndex < tokens.Length; tokenIndex++)
        {
            var token = tokens[tokenIndex];
            if (token.IsPunctuation &&
                token.Value is "." or "?" or "!" &&
                !token.Value.Equals(pattern[patternIndex], StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (token.IsQuoted ||
                (token.IsPunctuation &&
                 !token.Value.Equals(pattern[patternIndex], StringComparison.OrdinalIgnoreCase)))
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
        ImmutableArray<string> startAfterTokens,
        ImmutableArray<string> stopBeforeTokens,
        out string value)
    {
        value = string.Empty;
        if (lastMatchedToken < 0 || lastMatchedToken >= tokens.Length)
        {
            return false;
        }

        var startToken = lastMatchedToken;
        if (!startAfterTokens.IsDefaultOrEmpty)
        {
            if (!TryFindMatch(tokens, startAfterTokens, lastMatchedToken + 1, out startToken))
            {
                return false;
            }
        }

        var captureStart = tokens[startToken].Start + tokens[startToken].Length;
        if (captureStart >= input.Length)
        {
            return false;
        }

        var captureEnd = input.Length;
        for (var index = startToken + 1; index < tokens.Length; index++)
        {
            var token = tokens[index];
            if (!token.IsQuoted &&
                token.IsPunctuation &&
                token.Value is "." or "?" or "!")
            {
                captureEnd = token.Start + token.Length;
                break;
            }

            if (stopBeforeTokens.Any(stop =>
                token.Value.Equals(stop, StringComparison.OrdinalIgnoreCase)))
            {
                captureEnd = token.Start;
                break;
            }
        }

        var quotedValue = tokens
            .Skip(startToken + 1)
            .TakeWhile(token => token.Start < captureEnd)
            .FirstOrDefault(token => token.IsQuoted);
        if (quotedValue.IsQuoted)
        {
            value = quotedValue.Value;
            return value.Length > 0;
        }

        value = input[captureStart..captureEnd].Trim();
        if (value.EndsWith(".", StringComparison.Ordinal) ||
            value.EndsWith("?", StringComparison.Ordinal))
        {
            value = value[..^1].TrimEnd();
        }

        return value.Length > 0;
    }
}
