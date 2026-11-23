using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;
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
        var collections = new Dictionary<string, Dictionary<string, ImmutableArray<string>>>(StringComparer.Ordinal);
        var matchedAnyRule = false;

        foreach (var rule in knowledge.InputRules)
        {
            if (rule is null ||
                string.IsNullOrWhiteSpace(rule.Id) ||
                string.IsNullOrWhiteSpace(rule.FactId) ||
                rule.MatchTokens.IsDefaultOrEmpty ||
                !TryFindMatch(tokens, rule.MatchTokens, 0, out var lastMatchedToken, rule.SearchAllSentences))
            {
                continue;
            }

            var matchesRequiredSequence = rule.RequiredAnySequences.IsDefaultOrEmpty ||
                rule.RequiredAnySequences.Any(sequence =>
                    TryFindMatch(tokens, sequence, 0, out _, searchAllSentences: true));
            var matchesAllRequiredSequences = rule.RequiredAllSequences.IsDefaultOrEmpty ||
                rule.RequiredAllSequences.All(sequence =>
                    TryFindMatch(tokens, sequence, 0, out _, searchAllSentences: true));
            var matchesForbiddenSequence = !rule.ForbiddenAnySequences.IsDefaultOrEmpty &&
                rule.ForbiddenAnySequences.Any(sequence =>
                    TryFindMatch(tokens, sequence, 0, out _, searchAllSentences: true));
            if (!matchesRequiredSequence || !matchesAllRequiredSequences || matchesForbiddenSequence)
            {
                continue;
            }

            matchedAnyRule = true;
            if (rule.ValueId is null && rule.Captures.IsDefaultOrEmpty)
            {
                EnsureFact(extracted, rule.FactId);
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
                        ImmutableArray<ImmutableArray<string>>.Empty,
                        ImmutableArray<string>.Empty,
                        ImmutableArray<ImmutableArray<string>>.Empty,
                        ImmutableArray<string>.Empty,
                        string.Empty,
                        preserveRawValue: false,
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
                        capture.StartAfterSequences,
                        capture.StopBeforeTokens,
                        capture.StopBeforeSequences,
                        capture.TrimPrefixTokens,
                        capture.TrimSuffixCharacters,
                        capture.PreserveRawValue,
                        out capturedValue))
                {
                    continue;
                }

                if (capture.CollectionDelimiter is null && capture.CollectionDelimiters.IsDefaultOrEmpty)
                {
                    if (!TryNormalizeCapture(rule.Id, capture.Normalization, capturedValue, out capturedValue, out error))
                    {
                        return false;
                    }
                }

                if (capture.NumericOffset != 0)
                {
                    if (!int.TryParse(capturedValue, NumberStyles.None, CultureInfo.InvariantCulture, out var numericValue))
                    {
                        error = $"Input rule '{rule.Id}' applied a numeric offset to a non-integer value.";
                        return false;
                    }

                    try
                    {
                        capturedValue = checked(numericValue + capture.NumericOffset)
                            .ToString(CultureInfo.InvariantCulture);
                    }
                    catch (OverflowException)
                    {
                        error = $"Input rule '{rule.Id}' produced an integer outside the supported range.";
                        return false;
                    }
                }

                var specificity = rule.MatchTokens.Length +
                    (capture.StartAfterTokens.IsDefaultOrEmpty ? 0 : capture.StartAfterTokens.Length);
                var rawItems = !capture.CollectionDelimiters.IsDefaultOrEmpty
                    ? SplitText(capturedValue, capture.CollectionDelimiters)
                    : capture.CollectionDelimiter is { Length: > 0 } delimiter
                        ? capturedValue.Split(
                            delimiter,
                            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                            .ToImmutableArray()
                        : ImmutableArray<string>.Empty;

                if (!rawItems.IsDefaultOrEmpty)
                {
                    if (rawItems.IsDefaultOrEmpty)
                    {
                        error = $"Input rule '{rule.Id}' produced an empty ordered collection.";
                        return false;
                    }

                    var items = ImmutableArray.CreateBuilder<string>(rawItems.Length);
                    foreach (var rawItem in rawItems)
                    {
                        string itemValue;
                        if (capture.CollectionFieldDelimiter is { Length: > 0 } fieldDelimiter)
                        {
                            var fields = SplitText(rawItem, ImmutableArray.Create(fieldDelimiter));
                            if (fields.Length < 2 ||
                                (capture.CollectionFieldIndex is not null &&
                                 capture.CollectionFieldIndex.Value >= fields.Length))
                            {
                                error = $"Input rule '{rule.Id}' produced a collection item with missing fields.";
                                return false;
                            }

                            if (capture.CollectionTemplate is { } template)
                            {
                                if (fields.Length != 2)
                                {
                                    error = $"Input rule '{rule.Id}' produced an item with an unexpected number of fields.";
                                    return false;
                                }

                                itemValue = template.Replace("{0}", fields[0], StringComparison.Ordinal)
                                    .Replace("{1}", fields[1], StringComparison.Ordinal);
                            }
                            else
                            {
                                itemValue = fields[capture.CollectionFieldIndex!.Value];
                            }
                        }
                        else
                        {
                            itemValue = rawItem;
                        }

                        if (!TryNormalizeCapture(rule.Id, capture.Normalization, itemValue, out itemValue, out error))
                        {
                            return false;
                        }

                        items.Add(itemValue);
                    }

                    var immutableItems = items.ToImmutable();
                    if (immutableItems.Any(string.IsNullOrWhiteSpace))
                    {
                        error = $"Input rule '{rule.Id}' produced an empty collection value.";
                        return false;
                    }

                    EnsureFact(extracted, capture.FactId);
                    if (!TryAddCollection(collections, capture.FactId, capture.ValueId, immutableItems, out error))
                    {
                        return false;
                    }
                }
                else if (!TryAddValue(extracted, capture.FactId, capture.ValueId, capturedValue, specificity, out error))
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

        var extractedFacts = extracted
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => new SequenceSemanticFact(
                item.Key,
                item.Value.ToImmutableDictionary(
                    value => value.Key,
                    value => value.Value.Value,
                    StringComparer.Ordinal))
            {
                Collections = collections.TryGetValue(item.Key, out var factCollections)
                    ? factCollections.ToImmutableDictionary(StringComparer.Ordinal)
                    : ImmutableDictionary<string, ImmutableArray<string>>.Empty.WithComparers(StringComparer.Ordinal)
            })
            .ToImmutableArray();

        if (!SequenceInputFactValidator.TryValidate(knowledge.Validations, extractedFacts, out error))
        {
            return false;
        }

        facts = extractedFacts;
        return true;
    }

    private static bool TryNormalizeCapture(
        string ruleId,
        string? normalization,
        string value,
        out string normalized,
        out string? error)
    {
        normalized = value;
        error = null;
        if (normalization == "booleanExpression")
        {
            if (!BooleanExpressionNormalizer.TryNormalize(value, out normalized))
            {
                error = $"Input rule '{ruleId}' captured an invalid boolean expression.";
                return false;
            }
        }
        else if (normalization == "comparisonOperator")
        {
            if (!ComparisonOperatorNormalizer.TryNormalize(value, out normalized))
            {
                error = $"Input rule '{ruleId}' captured an unsupported comparison operator.";
                return false;
            }
        }

        return true;
    }

    private static void EnsureFact(
        Dictionary<string, Dictionary<string, (string Value, int Specificity)>> extracted,
        string factId)
    {
        if (!extracted.ContainsKey(factId))
        {
            extracted.Add(
                factId,
                new Dictionary<string, (string Value, int Specificity)>(StringComparer.Ordinal));
        }
    }

    private static bool TryAddCollection(
        Dictionary<string, Dictionary<string, ImmutableArray<string>>> extracted,
        string factId,
        string valueId,
        ImmutableArray<string> items,
        out string? error)
    {
        error = null;
        if (!extracted.TryGetValue(factId, out var values))
        {
            values = new Dictionary<string, ImmutableArray<string>>(StringComparer.Ordinal);
            extracted.Add(factId, values);
        }

        if (values.TryGetValue(valueId, out var existing))
        {
            if (!existing.SequenceEqual(items, StringComparer.Ordinal))
            {
                error = $"Input rules produced conflicting collections for semantic fact '{factId}'.";
                return false;
            }

            return true;
        }

        values.Add(valueId, items);
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
        out int lastMatchedToken,
        bool searchAllSentences = false,
        bool ignoreEmbeddedDecimalPoints = false)
    {
        lastMatchedToken = -1;
        var patternIndex = 0;

        for (var tokenIndex = startIndex; tokenIndex < tokens.Length; tokenIndex++)
        {
            var token = tokens[tokenIndex];
            var isEmbeddedDecimalPoint = token.Value == "." &&
                tokenIndex > 0 &&
                tokenIndex + 1 < tokens.Length &&
                tokens[tokenIndex - 1].Value.Length > 0 &&
                tokens[tokenIndex + 1].Value.Length > 0 &&
                tokens[tokenIndex - 1].Value.All(char.IsAsciiDigit) &&
                tokens[tokenIndex + 1].Value.All(char.IsAsciiDigit);
            if (token.IsPunctuation &&
                token.Value is "." or "?" or "!" &&
                (!ignoreEmbeddedDecimalPoints || !isEmbeddedDecimalPoint) &&
                !token.Value.Equals(pattern[patternIndex], StringComparison.OrdinalIgnoreCase))
            {
                if (!searchAllSentences)
                {
                    break;
                }

                patternIndex = 0;
                continue;
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
        ImmutableArray<ImmutableArray<string>> startAfterSequences,
        ImmutableArray<string> stopBeforeTokens,
        ImmutableArray<ImmutableArray<string>> stopBeforeSequences,
        ImmutableArray<string> trimPrefixTokens,
        string trimSuffixCharacters,
        bool preserveRawValue,
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
            if (!TryFindMatch(tokens, startAfterTokens, lastMatchedToken + 1, out startToken, ignoreEmbeddedDecimalPoints: true))
            {
                return false;
            }
        }
        else if (!startAfterSequences.IsDefaultOrEmpty)
        {
            var matchedStart = int.MaxValue;
            foreach (var sequence in startAfterSequences)
            {
                if (TryFindMatch(tokens, sequence, lastMatchedToken + 1, out var candidateStart, ignoreEmbeddedDecimalPoints: true) &&
                    candidateStart < matchedStart)
                {
                    matchedStart = candidateStart;
                }
            }

            if (matchedStart == int.MaxValue)
            {
                return false;
            }

            startToken = matchedStart;
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
                token.Value is "." or "?" or "!" &&
                !IsEmbeddedPeriod(input, token))
            {
                captureEnd = token.Value == "!"
                    ? token.Start + token.Length
                    : token.Start;
                break;
            }

            if ((!stopBeforeTokens.IsDefaultOrEmpty &&
                 stopBeforeTokens.Any(stop =>
                     token.Value.Equals(stop, StringComparison.OrdinalIgnoreCase))) ||
                (!stopBeforeSequences.IsDefaultOrEmpty &&
                 stopBeforeSequences.Any(sequence =>
                     MatchesSequenceAt(tokens, index, sequence))))
            {
                captureEnd = token.Start;
                break;
            }
        }

        var capturedText = input[captureStart..captureEnd].Trim();
        var isBracketedCollection = capturedText.Length >= 2 &&
            capturedText[0] == '[' &&
            capturedText[^1] == ']';
        var quotedValue = tokens
            .Skip(startToken + 1)
            .TakeWhile(token => token.Start < captureEnd)
            .FirstOrDefault(token => token.IsQuoted);
        if (preserveRawValue)
        {
            value = capturedText;
        }
        else if (quotedValue.IsQuoted && !isBracketedCollection)
        {
            value = quotedValue.Value;
        }
        else
        {
            value = capturedText;
            if (value.EndsWith(".", StringComparison.Ordinal) ||
                value.EndsWith("?", StringComparison.Ordinal))
            {
                value = value[..^1].TrimEnd();
            }
        }

        if (!trimPrefixTokens.IsDefaultOrEmpty)
        {
            var prefixEnd = value.IndexOfAny([' ', '\t', '\r', '\n']);
            var firstToken = prefixEnd < 0 ? value : value[..prefixEnd];
            if (trimPrefixTokens.Contains(firstToken, StringComparer.OrdinalIgnoreCase))
            {
                value = prefixEnd < 0 ? string.Empty : value[(prefixEnd + 1)..].TrimStart();
            }
        }

        if (trimSuffixCharacters.Length > 0)
        {
            value = value.TrimEnd().TrimEnd(trimSuffixCharacters.ToCharArray()).TrimEnd();
        }

        return value.Length > 0;
    }

    private static bool IsEmbeddedPeriod(string input, EnglishToken token) =>
        token.Value == "." &&
        token.Start > 0 &&
        token.Start + token.Length < input.Length &&
        char.IsLetterOrDigit(input[token.Start - 1]) &&
        char.IsLetterOrDigit(input[token.Start + token.Length]);

    private static bool MatchesSequenceAt(
        ImmutableArray<EnglishToken> tokens,
        int startIndex,
        ImmutableArray<string> sequence)
    {
        if (sequence.IsDefaultOrEmpty)
        {
            return false;
        }

        var patternIndex = 0;
        for (var index = startIndex; index < tokens.Length && patternIndex < sequence.Length; index++)
        {
            if (tokens[index].IsPunctuation || tokens[index].IsQuoted ||
                !tokens[index].Value.Equals(sequence[patternIndex], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            patternIndex++;
        }

        return patternIndex == sequence.Length;
    }

    private static ImmutableArray<string> SplitText(
        string value,
        ImmutableArray<string> delimiters)
    {
        var patterns = delimiters
            .Select(delimiter => delimiter.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(terms => terms.Length > 0)
            .Select(terms =>
            {
                var separator = terms.All(term => term.All(char.IsPunctuation)) ? @"\s*" : @"\s+";
                return separator + string.Join(separator, terms.Select(Regex.Escape)) + separator;
            })
            .ToArray();
        if (patterns.Length == 0)
        {
            return ImmutableArray<string>.Empty;
        }

        var pattern = "(?:" + string.Join("|", patterns) + ")";
        return Regex.Split(value, pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .ToImmutableArray();
    }
}
