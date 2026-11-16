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

        var bindings = ImmutableDictionary.CreateBuilder<string, ImmutableArray<string>>(
            StringComparer.Ordinal);

        foreach (var binding in pack.Bindings)
        {
            var sourceValue = binding.Source switch
            {
                "selected-operation" => selectedOperation!.Symbol,
                "goal-argument" => goal.ArgumentValue,
                _ => string.Empty
            };

            var value = binding.Encoding switch
            {
                "none" => sourceValue,
                "string-literal" => EncodeStringLiteral(sourceValue, pack.StringLiteralFormat),
                _ => string.Empty
            };

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
                pack.StartSymbol,
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
            selectedOperation!.Id,
            candidateAssessments);
        return true;
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

    private static string EncodeStringLiteral(
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
