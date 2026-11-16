using System.Collections.Immutable;
using System.Text;

namespace Lattice.Core.Generation;

public sealed class SequenceGenerator
{
    private const int MaximumExpansions = 256;
    private const int MaximumTokens = 256;

    private readonly GenerationOperationSelector operationSelector = new();

    public bool TryGenerate(
        GenerationLanguagePack pack,
        GenerationGoal goal,
        out SequenceGenerationResult? result,
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

        var resolvedBindings = new Dictionary<string, string>(StringComparer.Ordinal);
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

            if (value.Length == 0 || !resolvedBindings.TryAdd(binding.Name, value))
            {
                error = $"The generation binding '{binding.Name}' could not be resolved.";
                return false;
            }
        }

        var productions = pack.Productions.ToDictionary(
            production => production.Head,
            StringComparer.Ordinal);
        var pending = new Stack<GenerationSymbol>();
        pending.Push(new GenerationSymbol("nonterminal", pack.StartSymbol));
        var tokens = ImmutableArray.CreateBuilder<string>();
        var expansions = 0;

        while (pending.Count > 0)
        {
            if (tokens.Count >= MaximumTokens)
            {
                error = "The generated sequence exceeded the token limit.";
                return false;
            }

            var symbol = pending.Pop();
            switch (symbol.Kind)
            {
                case "terminal":
                    tokens.Add(symbol.Value);
                    break;
                case "binding":
                    if (!resolvedBindings.TryGetValue(symbol.Value, out var boundValue))
                    {
                        error = $"The grammar binding '{symbol.Value}' has no resolved value.";
                        return false;
                    }

                    tokens.Add(boundValue);
                    break;
                case "nonterminal":
                    if (++expansions > MaximumExpansions ||
                        !productions.TryGetValue(symbol.Value, out var production))
                    {
                        error = $"The grammar could not expand nonterminal '{symbol.Value}' within the generation limits.";
                        return false;
                    }

                    for (var index = production.Symbols.Length - 1; index >= 0; index--)
                    {
                        pending.Push(production.Symbols[index]);
                    }

                    break;
                default:
                    error = $"The grammar symbol kind '{symbol.Kind}' is unsupported.";
                    return false;
            }
        }

        if (tokens.Count == 0)
        {
            error = "The grammar generated an empty token sequence.";
            return false;
        }

        result = new SequenceGenerationResult(
            tokens.ToImmutable(),
            selectedOperation!.Id,
            candidateAssessments);
        return true;
    }

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
