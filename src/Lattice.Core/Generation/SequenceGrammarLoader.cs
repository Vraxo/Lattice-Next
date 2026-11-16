using System.Collections.Immutable;
using System.Text.Json;

namespace Lattice.Core.Generation;

/// <summary>
/// Loads language-agnostic sequence grammars from declarative JSON.
/// </summary>
public static class SequenceGrammarLoader
{
    private const int SupportedFormatVersion = 1;
    private const int MaximumJsonLength = 1_048_576;
    private const int MaximumProductions = 10_000;
    private const int MaximumSymbols = 65_536;
    private const int MaximumNameLength = 128;
    private const int MaximumTokenLength = 4096;

    public static bool TryLoad(
        string json,
        out SequenceGrammar? grammar,
        out string? error)
    {
        grammar = null;
        error = null;

        if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumJsonLength)
        {
            error = "A non-empty grammar document within the supported size limit is required.";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(
                json,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 32
                });

            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !HasOnlyProperties(document.RootElement, "formatVersion", "productions") ||
                !TryGetInt32(document.RootElement, "formatVersion", out var formatVersion) ||
                formatVersion != SupportedFormatVersion ||
                !document.RootElement.TryGetProperty("productions", out var productionElement) ||
                productionElement.ValueKind != JsonValueKind.Array ||
                productionElement.GetArrayLength() is 0 or > MaximumProductions)
            {
                error = "The grammar metadata or production collection is invalid or unsupported.";
                return false;
            }

            var productions = new List<SequenceProduction>(productionElement.GetArrayLength());
            var symbolCount = 0;

            foreach (var productionElementItem in productionElement.EnumerateArray())
            {
                if (productionElementItem.ValueKind != JsonValueKind.Object ||
                    !HasOnlyProperties(productionElementItem, "head", "weight", "expansion") ||
                    !TryGetString(productionElementItem, "head", out var head) ||
                    !IsValidName(head) ||
                    !TryGetArray(productionElementItem, "expansion", out var expansionElement) ||
                    !TryGetWeight(productionElementItem, out var weight))
                {
                    error = "A production requires a valid head, finite optional weight, and expansion array.";
                    return false;
                }

                var expansion = new List<SequenceSymbol>(expansionElement.GetArrayLength());
                foreach (var symbolElement in expansionElement.EnumerateArray())
                {
                    if (symbolElement.ValueKind != JsonValueKind.Object ||
                        !HasOnlyProperties(symbolElement, "kind", "value") ||
                        !TryGetString(symbolElement, "kind", out var kind) ||
                        !TryGetString(symbolElement, "value", out var value) ||
                        !IsValidSymbolValue(value))
                    {
                        error = $"Production '{head}' contains an invalid symbol.";
                        return false;
                    }

                    var symbolKind = kind switch
                    {
                        "terminal" => SequenceSymbolKind.Terminal,
                        "nonterminal" => SequenceSymbolKind.Nonterminal,
                        "binding" => SequenceSymbolKind.Binding,
                        _ => (SequenceSymbolKind?)null
                    };

                    if (symbolKind is null ||
                        (symbolKind != SequenceSymbolKind.Terminal && !IsValidName(value)))
                    {
                        error = $"Production '{head}' contains an unsupported symbol kind or name.";
                        return false;
                    }

                    expansion.Add(new SequenceSymbol(symbolKind.Value, value));
                    symbolCount++;
                    if (symbolCount > MaximumSymbols)
                    {
                        error = "The grammar contains too many symbols.";
                        return false;
                    }
                }

                productions.Add(new SequenceProduction(head, expansion.ToImmutableArray(), weight));
            }

            grammar = new SequenceGrammar(productions.ToImmutableArray());
            return true;
        }
        catch (JsonException)
        {
            error = "The grammar document is not valid JSON.";
            return false;
        }
    }

    private static bool HasOnlyProperties(JsonElement element, params string[] allowedNames)
    {
        var allowed = allowedNames.ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!allowed.Contains(property.Name) || !seen.Add(property.Name))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetInt32(JsonElement element, string name, out int value)
    {
        value = 0;
        return element.TryGetProperty(name, out var property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetInt32(out value);
    }

    private static bool TryGetString(JsonElement element, string name, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(name, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return true;
    }

    private static bool TryGetArray(
        JsonElement element,
        string name,
        out JsonElement value)
    {
        value = default;
        return element.TryGetProperty(name, out value) &&
            value.ValueKind == JsonValueKind.Array;
    }

    private static bool TryGetWeight(JsonElement element, out double value)
    {
        value = 0;
        if (!element.TryGetProperty("weight", out var property))
        {
            return true;
        }

        return property.ValueKind == JsonValueKind.Number &&
            property.TryGetDouble(out value) &&
            double.IsFinite(value);
    }

    private static bool IsValidName(string value) =>
        value.Length is > 0 and <= MaximumNameLength &&
        !string.IsNullOrWhiteSpace(value);

    private static bool IsValidSymbolValue(string value) =>
        value.Length is > 0 and <= MaximumTokenLength &&
        !string.IsNullOrEmpty(value);
}
