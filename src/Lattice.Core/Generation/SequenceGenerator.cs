using System.Collections.Immutable;

namespace Lattice.Core.Generation;

public enum SequenceSymbolKind
{
    Terminal,
    Nonterminal,
    Binding
}

public sealed record SequenceSymbol(SequenceSymbolKind Kind, string Value)
{
    public static SequenceSymbol Token(string value) =>
        new(SequenceSymbolKind.Terminal, value);

    public static SequenceSymbol Symbol(string name) =>
        new(SequenceSymbolKind.Nonterminal, name);

    public static SequenceSymbol Bound(string name) =>
        new(SequenceSymbolKind.Binding, name);
}

public sealed record SequenceProduction(
    string Head,
    ImmutableArray<SequenceSymbol> Expansion,
    double Weight = 0);

public sealed record SequenceGrammar(ImmutableArray<SequenceProduction> Productions);

public sealed record SequenceGenerationRequest(
    string StartSymbol,
    ImmutableDictionary<string, ImmutableArray<string>> Bindings,
    int BeamWidth = 8,
    int MaxSteps = 2048,
    int MaxTokens = 512);

public sealed record SequenceGenerationResult(
    ImmutableArray<string> Tokens,
    double Score,
    bool SearchLimitReached)
{
    public string Text => string.Concat(Tokens);
}

internal sealed record SequenceGenerationState(
    ImmutableArray<SequenceSymbol> Pending,
    ImmutableArray<string> Tokens,
    double Score);

public static class SequenceGenerator
{
    public static bool TryGenerate(
        SequenceGrammar grammar,
        SequenceGenerationRequest request,
        out SequenceGenerationResult? result,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(grammar);
        ArgumentNullException.ThrowIfNull(request);

        result = null;
        error = null;

        if (grammar.Productions.IsDefaultOrEmpty ||
            string.IsNullOrWhiteSpace(request.StartSymbol) ||
            request.Bindings is null)
        {
            error = "A grammar, start symbol, and initialized binding map are required.";
            return false;
        }

        if (request.BeamWidth is < 1 or > 128 ||
            request.MaxSteps is < 1 or > 10000 ||
            request.MaxTokens is < 1 or > 8192)
        {
            error = "Generation limits are outside their supported ranges.";
            return false;
        }

        var productions = new Dictionary<string, List<SequenceProduction>>(StringComparer.Ordinal);
        foreach (var production in grammar.Productions)
        {
            if (production is null ||
                string.IsNullOrWhiteSpace(production.Head) ||
                production.Expansion.IsDefault ||
                !double.IsFinite(production.Weight) ||
                production.Expansion.Any(symbol => !IsValidSymbol(symbol)))
            {
                error = "Every production requires a head, an initialized expansion, valid symbols, and a finite weight.";
                return false;
            }

            if (!productions.TryGetValue(production.Head, out var alternatives))
            {
                alternatives = [];
                productions.Add(production.Head, alternatives);
            }

            alternatives.Add(production);
        }

        if (!productions.ContainsKey(request.StartSymbol))
        {
            error = $"The grammar has no production for start symbol '{request.StartSymbol}'.";
            return false;
        }

        foreach (var production in grammar.Productions)
        {
            foreach (var symbol in production.Expansion)
            {
                if (symbol.Kind == SequenceSymbolKind.Nonterminal &&
                    !productions.ContainsKey(symbol.Value))
                {
                    error = $"The grammar references undefined symbol '{symbol.Value}'.";
                    return false;
                }
            }
        }

        foreach (var binding in request.Bindings)
        {
            if (string.IsNullOrWhiteSpace(binding.Key) ||
                binding.Value.IsDefault ||
                binding.Value.Any(string.IsNullOrEmpty))
            {
                error = $"Binding '{binding.Key}' must contain initialized, non-empty surface tokens.";
                return false;
            }
        }

        var frontier = new List<SequenceGenerationState>
        {
            new(
                ImmutableArray.Create(SequenceSymbol.Symbol(request.StartSymbol)),
                ImmutableArray<string>.Empty,
                0)
        };
        var completed = new List<SequenceGenerationState>();
        string? lastError = null;
        var steps = 0;

        for (; steps < request.MaxSteps && frontier.Count > 0; steps++)
        {
            var next = new List<SequenceGenerationState>();

            foreach (var state in frontier)
            {
                if (state.Pending.IsDefaultOrEmpty)
                {
                    completed.Add(state);
                    continue;
                }

                var current = state.Pending[0];
                var remainder = state.Pending.RemoveAt(0);

                switch (current.Kind)
                {
                    case SequenceSymbolKind.Terminal:
                        if (state.Tokens.Length >= request.MaxTokens)
                        {
                            lastError = "The generated sequence exceeded the token limit.";
                            continue;
                        }

                        next.Add(state with
                        {
                            Pending = remainder,
                            Tokens = state.Tokens.Add(current.Value)
                        });
                        break;

                    case SequenceSymbolKind.Binding:
                        if (!request.Bindings.TryGetValue(current.Value, out var boundTokens) ||
                            boundTokens.IsDefaultOrEmpty)
                        {
                            lastError = $"No surface tokens were supplied for binding '{current.Value}'.";
                            continue;
                        }

                        if (state.Tokens.Length + boundTokens.Length > request.MaxTokens)
                        {
                            lastError = "The generated sequence exceeded the token limit.";
                            continue;
                        }

                        next.Add(state with
                        {
                            Pending = remainder,
                            Tokens = state.Tokens.AddRange(boundTokens)
                        });
                        break;

                    case SequenceSymbolKind.Nonterminal:
                        if (!productions.TryGetValue(current.Value, out var alternatives))
                        {
                            lastError = $"The grammar has no production for symbol '{current.Value}'.";
                            continue;
                        }

                        foreach (var production in alternatives)
                        {
                            var score = state.Score + production.Weight;
                            if (!double.IsFinite(score))
                            {
                                lastError = "The accumulated production score is not finite.";
                                continue;
                            }

                            next.Add(new SequenceGenerationState(
                                production.Expansion.AddRange(remainder),
                                state.Tokens,
                                score));
                        }

                        break;

                    default:
                        lastError = $"Unsupported sequence symbol kind '{current.Kind}'.";
                        break;
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
                .ThenBy(state => BuildPendingKey(state.Pending), StringComparer.Ordinal)
                .Take(request.BeamWidth)
                .ToList();
        }

        foreach (var state in frontier.Where(state => state.Pending.IsDefaultOrEmpty))
        {
            completed.Add(state);
        }

        if (completed.Count == 0)
        {
            error = steps >= request.MaxSteps
                ? "Sequence generation reached its step limit without completing a sequence."
                : lastError ?? "No valid sequence could be generated from the supplied grammar and bindings.";
            return false;
        }

        var best = completed
            .OrderByDescending(state => state.Score)
            .ThenBy(state => BuildTokenKey(state.Tokens), StringComparer.Ordinal)
            .First();

        result = new SequenceGenerationResult(
            best.Tokens,
            best.Score,
            frontier.Count > 0);
        return true;
    }

    private static bool IsValidSymbol(SequenceSymbol? symbol)
    {
        if (symbol is null || !Enum.IsDefined(symbol.Kind) || string.IsNullOrEmpty(symbol.Value))
        {
            return false;
        }

        return symbol.Kind == SequenceSymbolKind.Terminal ||
            !string.IsNullOrWhiteSpace(symbol.Value);
    }

    private static string BuildTokenKey(ImmutableArray<string> tokens) =>
        string.Join("\u001f", tokens);

    private static string BuildPendingKey(ImmutableArray<SequenceSymbol> symbols) =>
        string.Join(
            "\u001f",
            symbols.Select(symbol => $"{(int)symbol.Kind}:{symbol.Value}"));
}
