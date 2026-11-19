using System.Collections.Immutable;

namespace Lattice.Core.Generation;

public sealed record KnowledgeDrivenSequenceGenerationRequest(
    ImmutableArray<string> ContextTokens,
    ImmutableArray<string> ActiveFacts,
    int BeamWidth = 8,
    int MaxTokens = 512);

public static class KnowledgeDrivenSequenceGenerator
{
    private const int MaximumFacts = 128;
    private const int MaximumContextTokens = 8192;
    private const int MaximumGeneratedTokens = 8192;

    private sealed record GenerationState(
        ImmutableArray<string> Context,
        ImmutableArray<string> Tokens,
        ImmutableHashSet<string> CompletedFacts,
        double Score);

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

        if (!TryValidate(knowledge, request, out var activeFacts, out var contextLength, out error))
        {
            return false;
        }

        if (request.BeamWidth is < 1 or > 128 ||
            request.MaxTokens is < 1 or > MaximumGeneratedTokens)
        {
            error = "The knowledge-driven generation limits are outside their supported ranges.";
            return false;
        }

        var initialContext = TrimContext(request.ContextTokens, contextLength);
        var frontier = new List<GenerationState>
        {
            new(
                initialContext,
                ImmutableArray<string>.Empty,
                ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal),
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

                var candidates = knowledge.Rules
                    .Where(rule =>
                        MatchesContext(rule.ContextSuffix, state.Context) &&
                        RequiredFactsAreActive(rule.RequiredFacts, activeFacts, state.CompletedFacts) &&
                        RequiredFactsAreComplete(rule.RequiresCompletedFacts, state.CompletedFacts) &&
                        SatisfiesOnlyActiveFacts(rule.SatisfiesFacts, activeFacts))
                    .ToArray();

                var modelCandidates = model is null
                    ? ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal)
                    : model.GetNextTokens(state.Context).ToImmutableHashSet(StringComparer.Ordinal);
                var hasModelCandidate = candidates.Any(rule => modelCandidates.Contains(rule.NextToken));

                foreach (var candidate in candidates)
                {
                    if (hasModelCandidate && !modelCandidates.Contains(candidate.NextToken))
                    {
                        continue;
                    }

                    var score = candidate.Weight + (model?.GetLogScore(state.Context, candidate.NextToken) ?? 0);
                    if (!double.IsFinite(score))
                    {
                        continue;
                    }

                    var nextContext = state.Context.Add(candidate.NextToken);
                    if (nextContext.Length > contextLength)
                    {
                        nextContext = contextLength == 0
                            ? ImmutableArray<string>.Empty
                            : nextContext.Skip(nextContext.Length - contextLength).ToImmutableArray();
                    }

                    var completedFacts = candidate.SatisfiesFacts.IsDefaultOrEmpty
                        ? state.CompletedFacts
                        : state.CompletedFacts.Union(candidate.SatisfiesFacts);

                    next.Add(new GenerationState(
                        nextContext,
                        state.Tokens.Add(candidate.NextToken),
                        completedFacts,
                        score));
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
        KnowledgeDrivenSequenceGenerationRequest request,
        out ImmutableHashSet<string> activeFacts,
        out int contextLength,
        out string? error)
    {
        activeFacts = ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal);
        contextLength = 0;
        error = null;

        if (knowledge.Rules.IsDefaultOrEmpty ||
            knowledge.Rules.Length > 10_000 ||
            request.ContextTokens.IsDefault ||
            request.ContextTokens.Length > MaximumContextTokens ||
            request.ContextTokens.Any(string.IsNullOrEmpty) ||
            request.ActiveFacts.IsDefaultOrEmpty ||
            request.ActiveFacts.Length > MaximumFacts ||
            request.ActiveFacts.Any(string.IsNullOrWhiteSpace) ||
            request.ActiveFacts.Distinct(StringComparer.Ordinal).Count() != request.ActiveFacts.Length)
        {
            error = "A non-empty knowledge base and one to 128 unique active semantic facts are required.";
            return false;
        }

        var ruleIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rule in knowledge.Rules)
        {
            if (rule is null ||
                string.IsNullOrWhiteSpace(rule.Id) ||
                rule.Id.Length > 128 ||
                !ruleIds.Add(rule.Id) ||
                string.IsNullOrEmpty(rule.NextToken) ||
                rule.NextToken.Length > 4096 ||
                !double.IsFinite(rule.Weight) ||
                rule.ContextSuffix.IsDefault ||
                rule.ContextSuffix.Length > 64 ||
                rule.ContextSuffix.Any(string.IsNullOrEmpty) ||
                !IsValidFactArray(rule.RequiredFacts) ||
                !IsValidFactArray(rule.RequiresCompletedFacts) ||
                !IsValidFactArray(rule.SatisfiesFacts))
            {
                error = "Every knowledge rule requires a unique identifier, valid token context, next token, fact conditions, and finite weight.";
                return false;
            }

            contextLength = Math.Max(contextLength, rule.ContextSuffix.Length);
        }

        activeFacts = request.ActiveFacts.ToImmutableHashSet(StringComparer.Ordinal);
        contextLength = Math.Max(contextLength, 0);
        return true;
    }

    private static bool IsValidFactArray(ImmutableArray<string> facts) =>
        facts.IsDefault ||
        (facts.Length <= MaximumFacts &&
         facts.All(fact => !string.IsNullOrWhiteSpace(fact) && fact.Length <= 128) &&
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
            if (!string.Equals(context[offset + index], suffix[index], StringComparison.Ordinal))
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
