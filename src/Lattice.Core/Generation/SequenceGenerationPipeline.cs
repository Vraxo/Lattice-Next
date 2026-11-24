using System.Collections.Immutable;
using System.Text;

namespace Lattice.Core.Generation;

/// <summary>
/// Connects semantic generation goals and declarative language knowledge to the
/// format-agnostic sequence generation engine.
/// </summary>
public sealed class SequenceGenerationPipeline
{
    private readonly GenerationOperationSelector operationSelector = new();

    public bool TryGenerate(
        GenerationLanguagePack pack,
        GenerationGoal goal,
        out SequenceGenerationPipelineResult? result,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(goal);

        result = null;
        error = null;

        if (!operationSelector.TrySelect(
            pack.Operations,
            goal,
            out var selectedOperation,
            out var candidateAssessments,
            out error))
        {
            return false;
        }

        var startSymbol = selectedOperation!.StartSymbol ?? pack.StartSymbol;
        var requiredBindings = GetReachableBindings(pack.Productions, startSymbol);
        var bindings = ImmutableDictionary.CreateBuilder<string, ImmutableArray<string>>(
            StringComparer.Ordinal);

        foreach (var binding in pack.Bindings.Where(binding => requiredBindings.Contains(binding.Name)))
        {
            string value;
            if (binding.Source == "goal-collection")
            {
                if (binding.Encoding != "collection-literal" ||
                    binding.Key is null ||
                    !goal.CollectionBindings.TryGetValue(binding.Key, out var collection) ||
                    !SemanticCollectionLiteralRenderer.TryRender(collection, pack, out value))
                {
                    error = $"The generation binding '{binding.Name}' could not render its semantic collection.";
                    return false;
                }
            }
            else
            {
                var sourceValue = binding.Source switch
                {
                    "selected-operation" => selectedOperation!.Symbol,
                    "goal-argument" => goal.ArgumentValue,
                    "goal-binding" => binding.Key is { } key && goal.Bindings.TryGetValue(key, out var boundValue)
                        ? boundValue
                        : string.Empty,
                    _ => string.Empty
                };

                value = binding.Encoding switch
                {
                    "none" => sourceValue,
                    "string-literal" => EncodeStringLiteral(sourceValue, pack.StringLiteralFormat),
                    _ => string.Empty
                };
            }

            if (value.Length == 0 ||
                !bindings.TryAdd(binding.Name, ImmutableArray.Create(value)))
            {
                error = $"The generation binding '{binding.Name}' could not be resolved.";
                return false;
            }
        }

        var grammar = new SequenceGrammar(pack.Productions
            .Select(production => new SequenceProduction(
                production.Head,
                production.Symbols.Select(ToSequenceSymbol).ToImmutableArray()))
            .ToImmutableArray());

        if (!SequenceGenerator.TryGenerate(
            grammar,
            new SequenceGenerationRequest(
                startSymbol,
                bindings.ToImmutable(),
                MaxSteps: 256,
                MaxTokens: 256),
            out var generated,
            out error))
        {
            return false;
        }

        result = new SequenceGenerationPipelineResult(
            generated!,
            selectedOperation.Id,
            candidateAssessments);
        return true;
    }

    public bool TryGenerateProgram(
        GenerationLanguagePack pack,
        ImmutableArray<GenerationGoal> goals,
        out SequenceProgramGenerationResult? result,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(pack);

        result = null;
        error = null;

        if (goals.IsDefaultOrEmpty || goals.Length > 64)
        {
            error = "A program requires between one and 64 semantic generation steps.";
            return false;
        }

        var statements = ImmutableArray.CreateBuilder<SequenceGenerationPipelineResult>(goals.Length);
        foreach (var goal in goals)
        {
            if (!TryGenerate(pack, goal, out var statement, out error))
            {
                return false;
            }

            if (statement!.SearchLimitReached)
            {
                error = "A program statement reached the sequence generation search limit.";
                return false;
            }

            statements.Add(statement);
        }

        result = new SequenceProgramGenerationResult(statements.ToImmutable());
        return true;
    }

    private static HashSet<string> GetReachableBindings(
        ImmutableArray<GenerationProduction> productions,
        string startSymbol)
    {
        var productionMap = productions.ToDictionary(
            production => production.Head,
            StringComparer.Ordinal);
        var bindings = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>();
        pending.Push(startSymbol);

        while (pending.TryPop(out var current))
        {
            if (!visited.Add(current) || !productionMap.TryGetValue(current, out var production))
            {
                continue;
            }

            foreach (var symbol in production.Symbols)
            {
                if (symbol.Kind == "binding")
                {
                    bindings.Add(symbol.Value);
                }
                else if (symbol.Kind == "nonterminal")
                {
                    pending.Push(symbol.Value);
                }
            }
        }

        return bindings;
    }

    private static SequenceSymbol ToSequenceSymbol(GenerationSymbol symbol) =>
        symbol.Kind switch
        {
            "terminal" => SequenceSymbol.Token(symbol.Value),
            "nonterminal" => SequenceSymbol.Symbol(symbol.Value),
            "binding" => SequenceSymbol.Bound(symbol.Value),
            _ => throw new InvalidOperationException(
                $"The validated generation symbol kind '{symbol.Kind}' is unsupported.")
        };

    internal static string EncodeStringLiteral(
        string value,
        GenerationStringLiteralFormat format)
    {
        var replacements = format.Escapes.ToDictionary(
            escape => escape.Character[0],
            escape => escape.Replacement);

        var builder = new StringBuilder(
            format.OpeningDelimiter.Length + value.Length + format.ClosingDelimiter.Length);
        builder.Append(format.OpeningDelimiter);

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

        builder.Append(format.ClosingDelimiter);
        return builder.ToString();
    }
}

public sealed record SequenceGenerationPipelineResult(
    SequenceGenerationResult Generation,
    string SelectedOperationId,
    ImmutableArray<GenerationCandidateAssessment> CandidateAssessments)
{
    public ImmutableArray<string> Tokens => Generation.Tokens;

    public string Text => Generation.Text;

    public double Score => Generation.Score;

    public bool SearchLimitReached => Generation.SearchLimitReached;
}

public sealed record SequenceProgramGenerationResult(
    ImmutableArray<SequenceGenerationPipelineResult> Statements)
{
    public string Text => string.Join(Environment.NewLine, Statements.Select(statement => statement.Text));
}
