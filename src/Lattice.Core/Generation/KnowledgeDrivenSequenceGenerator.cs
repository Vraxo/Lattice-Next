using System.Collections.Immutable;
using System.Text;

namespace Lattice.Core.Generation;

public sealed record KnowledgeDrivenSequenceGenerationRequest(
    ImmutableArray<string> ContextTokens,
    ImmutableArray<string> ActiveFacts,
    int BeamWidth = 8,
    int MaxTokens = 512)
{
    public ImmutableArray<SequenceSemanticFact> FactValues { get; init; } =
        ImmutableArray<SequenceSemanticFact>.Empty;
}

public static class KnowledgeDrivenSequenceGenerator
{
    private const int MaximumFacts = 128;
    private const int MaximumContextTokens = 8192;
    private const int MaximumGeneratedTokens = 8192;
    private const int MaximumFactValueLength = 16_384;

    private sealed record GenerationState(
        ImmutableArray<string> Context,
        ImmutableArray<string> Tokens,
        ImmutableHashSet<string> CompletedFacts,
        ImmutableDictionary<string, int> CollectionCursors,
        double Score);

    private sealed record TokenCandidate(
        SequenceTokenRule Rule,
        string Token);

    public static bool TryGenerate(
        SequenceKnowledgeBase knowledge,
        SequenceTokenModel? model,
        KnowledgeDrivenSequenceGenerationRequest request,
        out SequenceGenerationResult? result,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(knowledge);
        ArgumentNullException.ThrowIfNull(request);

        result = null;
        error = null;

        if (!TryValidate(
            knowledge,
            model,
            request,
            out var facts,
            out var activeFacts,
            out var encodings,
            out var contextLength,
            out error))
        {
            return false;
        }

        if (request.BeamWidth is < 1 or > 128 ||
            request.MaxTokens is < 1 or > MaximumGeneratedTokens)
        {
            error = "The knowledge-driven generation limits are outside their supported ranges.";
            return false;
        }

        var initialContext = TrimContext(
            request.ContextTokens.Add(SequenceKnowledgeBase.StartToken),
            contextLength);
        var frontier = new List<GenerationState>
        {
            new(
                initialContext,
                ImmutableArray<string>.Empty,
                ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal),
                ImmutableDictionary<string, int>.Empty.WithComparers(StringComparer.Ordinal),
                0)
        };
        var completed = new List<GenerationState>();
        var steps = 0;

        for (; steps < request.MaxTokens && frontier.Count > 0; steps++)
        {
            var next = new List<GenerationState>();
            foreach (var state in frontier)
            {
                if (AllFactsSatisfied(activeFacts, state.CompletedFacts))
                {
                    completed.Add(state);
                    continue;
                }

                var candidates = new List<TokenCandidate>();
                foreach (var rule in knowledge.Rules)
                {
                    if (!MatchesContext(rule.ContextSuffix, state.Context) ||
                        !RequiredFactsAreActive(rule.RequiredFacts, activeFacts, state.CompletedFacts) ||
                        !RequiredFactsAreComplete(rule.RequiresCompletedFacts, state.CompletedFacts) ||
                        HasForbiddenActiveFact(rule.ForbiddenFacts, activeFacts) ||
                        !SatisfiesOnlyActiveFacts(rule.SatisfiesFacts, activeFacts) ||
                        !TryResolveToken(rule, facts, state.CollectionCursors, encodings, out var token))
                    {
                        continue;
                    }

                    candidates.Add(new TokenCandidate(rule, token));
                }

                var modelCandidates = model is null
                    ? ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal)
                    : model.GetNextTokens(state.Context).ToImmutableHashSet(StringComparer.Ordinal);
                var hasModelCandidate = candidates.Any(candidate =>
                    modelCandidates.Contains(candidate.Token));

                foreach (var candidate in candidates)
                {
                    if (hasModelCandidate && !modelCandidates.Contains(candidate.Token))
                    {
                        continue;
                    }

                    var score = state.Score + candidate.Rule.Weight +
                        (model?.GetLogScore(state.Context, candidate.Token) ?? 0);
                    if (!double.IsFinite(score))
                    {
                        continue;
                    }

                    var nextContext = state.Context.Add(candidate.Token);
                    if (nextContext.Length > contextLength)
                    {
                        nextContext = contextLength == 0
                            ? ImmutableArray<string>.Empty
                            : nextContext.Skip(nextContext.Length - contextLength).ToImmutableArray();
                    }

                    var completedFacts = candidate.Rule.SatisfiesFacts.IsDefaultOrEmpty
                        ? state.CompletedFacts
                        : state.CompletedFacts.Union(candidate.Rule.SatisfiesFacts);

                    if (!TryAdvanceCollections(candidate.Rule.AdvanceCollections, facts, state.CollectionCursors, completedFacts, out var cursors, out completedFacts))
                    {
                        continue;
                    }

                    next.Add(new GenerationState(nextContext, state.Tokens.Add(candidate.Token), completedFacts, cursors, score));
                }
            }

            if (completed.Count > request.BeamWidth * 4)
            {
                completed = completed
                    .OrderByDescending(state => state.Score)
                    .ThenBy(state => BuildTokenKey(state.Tokens), StringComparer.Ordinal)
                    .Take(request.BeamWidth * 4)
                    .ToList();
            }

            frontier = next
                .OrderByDescending(state => state.Score)
                .ThenBy(state => BuildTokenKey(state.Tokens), StringComparer.Ordinal)
                .ThenBy(state => BuildFactKey(state.CompletedFacts), StringComparer.Ordinal)
                .ThenBy(state => BuildCursorKey(state.CollectionCursors), StringComparer.Ordinal)
                .Take(request.BeamWidth)
                .ToList();
        }

        completed.AddRange(frontier.Where(state => AllFactsSatisfied(activeFacts, state.CompletedFacts)));
        if (completed.Count == 0)
        {
            error = steps >= request.MaxTokens
                ? "Knowledge-driven generation reached its token limit before satisfying every active fact."
                : "The knowledge base could not produce a sequence satisfying every active fact.";
            return false;
        }

        var best = completed
            .OrderByDescending(state => state.Score)
            .ThenBy(state => BuildTokenKey(state.Tokens), StringComparer.Ordinal)
            .First();

        result = new SequenceGenerationResult(
            best.Tokens,
            best.Score,
            steps >= request.MaxTokens && frontier.Any(state =>
                !AllFactsSatisfied(activeFacts, state.CompletedFacts)));
        return true;
    }

    private static bool TryValidate(
        SequenceKnowledgeBase knowledge,
        SequenceTokenModel? model,
        KnowledgeDrivenSequenceGenerationRequest request,
        out ImmutableDictionary<string, SequenceSemanticFact> facts,
        out ImmutableHashSet<string> activeFacts,
        out ImmutableDictionary<string, SequenceTokenEncoding> encodings,
        out int contextLength,
        out string? error)
    {
        facts = ImmutableDictionary<string, SequenceSemanticFact>.Empty.WithComparers(StringComparer.Ordinal);
        activeFacts = ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal);
        encodings = ImmutableDictionary<string, SequenceTokenEncoding>.Empty.WithComparers(StringComparer.Ordinal);
        contextLength = 0;
        error = null;

        if (knowledge.Rules.IsDefaultOrEmpty ||
            knowledge.Rules.Length > 10_000 ||
            request.ContextTokens.IsDefault ||
            request.ContextTokens.Length > MaximumContextTokens ||
            request.ContextTokens.Any(string.IsNullOrEmpty) ||
            request.ActiveFacts.IsDefaultOrEmpty ||
            request.ActiveFacts.Length > MaximumFacts ||
            request.ActiveFacts.Any(fact => string.IsNullOrWhiteSpace(fact) || fact.Length > 256) ||
            request.ActiveFacts.Distinct(StringComparer.Ordinal).Count() != request.ActiveFacts.Length ||
            request.FactValues.IsDefault ||
            request.FactValues.Length > MaximumFacts)
        {
            error = "A non-empty knowledge base, initialized context, and one to 128 unique active semantic facts are required.";
            return false;
        }

        activeFacts = request.ActiveFacts.ToImmutableHashSet(StringComparer.Ordinal);
        var factBuilder = ImmutableDictionary.CreateBuilder<string, SequenceSemanticFact>(StringComparer.Ordinal);
        foreach (var fact in request.FactValues)
        {
            if (fact is null ||
                !activeFacts.Contains(fact.Id) ||
                fact.Values is null ||
                !factBuilder.TryAdd(fact.Id, fact) ||
                fact.Values.Any(value =>
                    string.IsNullOrWhiteSpace(value.Key) ||
                    value.Key.Length > 128 ||
                    value.Value is null ||
                    value.Value.Length > MaximumFactValueLength) ||
                fact.Collections.Any(collection =>
                    string.IsNullOrWhiteSpace(collection.Key) || collection.Key.Length > 128 ||
                    collection.Value.IsDefault || collection.Value.Length > MaximumFacts ||
                    collection.Value.Any(value => value is null || value.Length > MaximumFactValueLength)))
            {
                error = "Every semantic value binding must reference one active fact and contain valid named values.";
                return false;
            }
        }

        facts = factBuilder.ToImmutable();

        var encodingBuilder = ImmutableDictionary.CreateBuilder<string, SequenceTokenEncoding>(StringComparer.Ordinal);
        var availableEncodings = knowledge.Encodings.IsDefault
            ? ImmutableArray<SequenceTokenEncoding>.Empty
            : knowledge.Encodings;
        foreach (var encoding in availableEncodings)
        {
            if (encoding is null ||
                !IsValidIdentifier(encoding.Id) ||
                !IsValidDelimiter(encoding.OpeningDelimiter) ||
                !IsValidDelimiter(encoding.ClosingDelimiter) ||
                encoding.Escapes.IsDefault ||
                encoding.Escapes.Any(escape =>
                    escape is null ||
                    escape.Character is null ||
                    escape.Character.Length != 1 ||
                    escape.Replacement is null) ||
                encoding.Escapes.Select(escape => escape.Character)
                    .Distinct(StringComparer.Ordinal).Count() != encoding.Escapes.Length ||
                !encodingBuilder.TryAdd(encoding.Id, encoding))
            {
                error = "Every token encoding requires a unique identifier, delimiters, and unique single-character escape rules.";
                return false;
            }
        }

        encodings = encodingBuilder.ToImmutable();

        var ruleIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rule in knowledge.Rules)
        {
            var hasLiteral = !string.IsNullOrEmpty(rule?.NextToken);
            var hasBinding = rule?.TokenBinding is not null;
            if (rule is null ||
                !IsValidIdentifier(rule.Id) ||
                !ruleIds.Add(rule.Id) ||
                hasLiteral == hasBinding ||
                (hasLiteral &&
                    (rule.NextToken!.Length > 4096 ||
                     rule.NextToken == SequenceKnowledgeBase.StartToken ||
                     rule.NextToken == SequenceTokenModel.EndToken)) ||
                !double.IsFinite(rule.Weight) ||
                rule.ContextSuffix.IsDefault ||
                rule.ContextSuffix.Length > 64 ||
                rule.ContextSuffix.Any(string.IsNullOrEmpty) ||
                !IsValidFactArray(rule.RequiredFacts) ||
                !IsValidFactArray(rule.RequiresCompletedFacts) ||
                !IsValidFactArray(rule.SatisfiesFacts) ||
                !IsValidFactArray(rule.ForbiddenFacts) ||
                (!rule.AdvanceCollections.IsDefault && rule.AdvanceCollections.Any(key =>
                    !IsValidCollectionKey(key))) ||
                (rule.TokenBinding is not null &&
                    (!IsValidIdentifier(rule.TokenBinding.FactId) ||
                     !IsValidIdentifier(rule.TokenBinding.ValueId) ||
                     !rule.RequiredFacts.Contains(rule.TokenBinding.FactId, StringComparer.Ordinal) ||
                     (rule.TokenBinding.UseCollection && rule.TokenBinding.Encoding is not null) ||
                     (rule.TokenBinding.CollectionSeparator is not null &&
                      (rule.TokenBinding.UseCollection ||
                       rule.TokenBinding.Encoding is not null ||
                       rule.TokenBinding.CollectionSeparator.Length > 32)) ||
                     rule.TokenBinding.Prefix is null ||
                     rule.TokenBinding.Suffix is null ||
                     rule.TokenBinding.Prefix.Length > 128 ||
                     rule.TokenBinding.Suffix.Length > 128 ||
                     rule.TokenBinding.MinimumCollectionCursor is < 0 or > MaximumFacts ||
                     (rule.TokenBinding.MinimumCollectionCursor > 0 && !rule.TokenBinding.UseCollection) ||
                     (rule.TokenBinding.ValueTemplate is null &&
                      (rule.TokenBinding.SecondaryValueId is not null ||
                       rule.TokenBinding.SecondaryCollectionSeparator is not null)) ||
                     (rule.TokenBinding.ValueTemplate is not null &&
                      (rule.TokenBinding.SecondaryValueId is null ||
                       rule.TokenBinding.SecondaryCollectionSeparator is null ||
                       !IsValidIdentifier(rule.TokenBinding.SecondaryValueId) ||
                       rule.TokenBinding.SecondaryCollectionSeparator.Length > 32 ||
                       rule.TokenBinding.ValueTemplate.Length > 256 ||
                       !rule.TokenBinding.ValueTemplate.Contains("{0}", StringComparison.Ordinal) ||
                       !rule.TokenBinding.ValueTemplate.Contains("{1}", StringComparison.Ordinal) ||
                       rule.TokenBinding.UseCollection ||
                       rule.TokenBinding.CollectionSeparator is not null ||
                       rule.TokenBinding.Encoding is not null)) ||
                     (rule.TokenBinding.Encoding is not null &&
                      !encodings.ContainsKey(rule.TokenBinding.Encoding)))))
            {
                error = "Every knowledge rule requires a unique identifier, exactly one valid token source, valid fact conditions, and finite weight.";
                return false;
            }

            contextLength = Math.Max(contextLength, rule.ContextSuffix.Length);
        }

        contextLength = Math.Max(contextLength, model?.MaximumContextLength ?? 0);
        return true;
    }

    private static bool TryResolveToken(
        SequenceTokenRule rule,
        ImmutableDictionary<string, SequenceSemanticFact> facts,
        ImmutableDictionary<string, int> collectionCursors,
        ImmutableDictionary<string, SequenceTokenEncoding> encodings,
        out string token)
    {
        token = string.Empty;
        if (rule.TokenBinding is null)
        {
            token = rule.NextToken ?? string.Empty;
            return token.Length > 0;
        }

        var binding = rule.TokenBinding;
        if (!facts.TryGetValue(binding.FactId, out var fact))
        {
            return false;
        }

        string value;
        if (binding.ValueTemplate is { } valueTemplate)
        {
            if (!fact.Values.TryGetValue(binding.ValueId, out var firstValue) ||
                !fact.Collections.TryGetValue(binding.SecondaryValueId!, out var secondaryValues) ||
                secondaryValues.IsDefaultOrEmpty)
            {
                return false;
            }

            var joinedValues = string.Join(binding.SecondaryCollectionSeparator!, secondaryValues);
            value = valueTemplate.Replace("{0}", firstValue, StringComparison.Ordinal)
                .Replace("{1}", joinedValues, StringComparison.Ordinal);
        }
        else if (binding.CollectionSeparator is { } separator)
        {
            if (!fact.Collections.TryGetValue(binding.ValueId, out var collection) ||
                collection.IsDefaultOrEmpty)
            {
                return false;
            }

            value = string.Join(separator, collection);
        }
        else if (binding.UseCollection)
        {
            var key = BuildCollectionKey(binding.FactId, binding.ValueId);
            var index = collectionCursors.GetValueOrDefault(key);
            if (index < binding.MinimumCollectionCursor ||
                !fact.Collections.TryGetValue(binding.ValueId, out var collection) ||
                index >= collection.Length)
            {
                return false;
            }

            value = collection[index];
        }
        else if (!fact.Values.TryGetValue(binding.ValueId, out value!))
        {
            return false;
        }

        var encodedValue = binding.Encoding is null ? value : EncodeValue(value, encodings[binding.Encoding]);
        token = binding.Prefix + encodedValue + binding.Suffix;

        return token.Length > 0 &&
            token.Length <= 4096 &&
            token != SequenceKnowledgeBase.StartToken &&
            token != SequenceTokenModel.EndToken;
    }

    private static bool TryAdvanceCollections(ImmutableArray<string> keys, ImmutableDictionary<string, SequenceSemanticFact> facts, ImmutableDictionary<string, int> current, ImmutableHashSet<string> currentCompleted, out ImmutableDictionary<string, int> cursors, out ImmutableHashSet<string> completed)
    {
        cursors = current;
        completed = currentCompleted;
        if (keys.IsDefaultOrEmpty) return true;
        var builder = current.ToBuilder();
        foreach (var key in keys)
        {
            var split = key.IndexOf(':');
            if (split <= 0) return false;
            var factId = key[..split];
            var valueId = key[(split + 1)..];
            if (!facts.TryGetValue(factId, out var fact) || !fact.Collections.TryGetValue(valueId, out var items) || items.IsDefaultOrEmpty) return false;
            var next = builder.GetValueOrDefault(key) + 1;
            if (next > items.Length) return false;
            builder[key] = next;
            if (fact.Collections.All(pair => builder.GetValueOrDefault($"{factId}:{pair.Key}") >= pair.Value.Length))
                completed = completed.Add(factId);
        }
        cursors = builder.ToImmutable();
        return true;
    }

    private static string BuildCollectionKey(string factId, string valueId) => $"{factId}:{valueId}";
    private static string BuildCursorKey(ImmutableDictionary<string, int> cursors) =>
        string.Join("\u001f", cursors.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}"));

    private static string EncodeValue(string value, SequenceTokenEncoding encoding)
    {
        var replacements = encoding.Escapes.ToDictionary(
            escape => escape.Character[0],
            escape => escape.Replacement);
        var builder = new StringBuilder(
            encoding.OpeningDelimiter.Length + value.Length + encoding.ClosingDelimiter.Length);
        builder.Append(encoding.OpeningDelimiter);

        foreach (var character in value)
        {
            if (replacements.TryGetValue(character, out var replacement))
            {
                builder.Append(replacement);
            }
            else
            {
                builder.Append(character);
            }
        }

        builder.Append(encoding.ClosingDelimiter);
        return builder.ToString();
    }

    private static bool IsValidCollectionKey(string value)
    {
        var separator = value.IndexOf(':');
        return separator > 0 && separator < value.Length - 1 &&
            IsValidIdentifier(value[..separator]) && IsValidIdentifier(value[(separator + 1)..]);
    }

    private static bool IsValidIdentifier(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 128 &&
        value.All(character =>
            char.IsAsciiLetterOrDigit(character) ||
            character is '.' or '-' or '_' or ':');

    private static bool IsValidDelimiter(string value) =>
        !string.IsNullOrEmpty(value) && value.Length <= 32;

    private static bool IsValidFactArray(ImmutableArray<string> facts) =>
        facts.IsDefault ||
        (facts.Length <= MaximumFacts &&
         facts.All(IsValidIdentifier) &&
         facts.Distinct(StringComparer.Ordinal).Count() == facts.Length);

    private static bool MatchesContext(
        ImmutableArray<string> suffix,
        ImmutableArray<string> context)
    {
        if (suffix.IsDefaultOrEmpty)
        {
            return true;
        }

        if (suffix.Length > context.Length)
        {
            return false;
        }

        var offset = context.Length - suffix.Length;
        for (var index = 0; index < suffix.Length; index++)
        {
            var expected = suffix[index];
            var actual = context[offset + index];
            if (expected == "*")
            {
                continue;
            }

            if (expected.StartsWith("*", StringComparison.Ordinal))
            {
                if (!actual.EndsWith(expected[1..], StringComparison.Ordinal))
                {
                    return false;
                }

                continue;
            }

            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool RequiredFactsAreActive(
        ImmutableArray<string> requiredFacts,
        ImmutableHashSet<string> activeFacts,
        ImmutableHashSet<string> completedFacts)
    {
        if (requiredFacts.IsDefaultOrEmpty)
        {
            return true;
        }

        return requiredFacts.All(fact =>
            activeFacts.Contains(fact) && !completedFacts.Contains(fact));
    }

    private static bool RequiredFactsAreComplete(
        ImmutableArray<string> requiredFacts,
        ImmutableHashSet<string> completedFacts) =>
        requiredFacts.IsDefaultOrEmpty || requiredFacts.All(completedFacts.Contains);

    private static bool HasForbiddenActiveFact(
        ImmutableArray<string> forbiddenFacts,
        ImmutableHashSet<string> activeFacts) =>
        !forbiddenFacts.IsDefaultOrEmpty &&
        forbiddenFacts.Any(activeFacts.Contains);

    private static bool SatisfiesOnlyActiveFacts(
        ImmutableArray<string> satisfiedFacts,
        ImmutableHashSet<string> activeFacts) =>
        satisfiedFacts.IsDefaultOrEmpty || satisfiedFacts.All(activeFacts.Contains);

    private static bool AllFactsSatisfied(
        ImmutableHashSet<string> activeFacts,
        ImmutableHashSet<string> completedFacts) =>
        activeFacts.IsSubsetOf(completedFacts);

    private static ImmutableArray<string> TrimContext(
        ImmutableArray<string> context,
        int maximumLength) =>
        maximumLength == 0
            ? ImmutableArray<string>.Empty
            : context.Length <= maximumLength
                ? context
                : context.Skip(context.Length - maximumLength).ToImmutableArray();

    private static string BuildTokenKey(ImmutableArray<string> tokens) =>
        string.Join("\u001f", tokens);

    private static string BuildFactKey(ImmutableHashSet<string> facts) =>
        string.Join("\u001f", facts.Order(StringComparer.Ordinal));
}
