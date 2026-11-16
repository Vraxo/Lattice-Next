using System.Collections.Immutable;
using System.Text.Json;

namespace Lattice.Core.Generation;

public sealed class GenerationLanguagePackLoader
{
    private const string ResourcePrefix = "Lattice.Core.GenerationKnowledge.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
    };

    public bool TryLoad(
        string? json,
        out GenerationLanguagePack? pack,
        out string? error)
    {
        pack = null;
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "The generation language pack is empty.";
            return false;
        }

        try
        {
            pack = JsonSerializer.Deserialize<GenerationLanguagePack>(json, JsonOptions);
        }
        catch (JsonException)
        {
            error = "The generation language pack is invalid JSON or contains an unsupported field.";
            return false;
        }

        if (pack is null ||
            pack.FormatVersion != 1 ||
            !IsIdentifier(pack.Language) ||
            !IsIdentifier(pack.StartSymbol) ||
            pack.Productions.IsDefaultOrEmpty ||
            pack.Productions.Length > 128 ||
            pack.Bindings.IsDefaultOrEmpty ||
            pack.Bindings.Length > 32 ||
            pack.Operations.IsDefaultOrEmpty ||
            pack.Operations.Length > 128 ||
            pack.StringLiteralFormat is null ||
            string.IsNullOrEmpty(pack.StringLiteralFormat.OpeningDelimiter) ||
            string.IsNullOrEmpty(pack.StringLiteralFormat.ClosingDelimiter) ||
            pack.StringLiteralFormat.OpeningDelimiter.Length > 4 ||
            pack.StringLiteralFormat.ClosingDelimiter.Length > 4 ||
            pack.StringLiteralFormat.Escapes.IsDefault)
        {
            error = "The generation language pack metadata or collection sizes are invalid.";
            pack = null;
            return false;
        }

        var productions = new Dictionary<string, GenerationProduction>(StringComparer.Ordinal);
        foreach (var production in pack.Productions)
        {
            if (production is null ||
                !IsIdentifier(production.Head) ||
                production.Symbols.IsDefaultOrEmpty ||
                production.Symbols.Length > 64 ||
                !productions.TryAdd(production.Head, production))
            {
                error = "Every grammar production requires a unique head and a non-empty symbol sequence.";
                pack = null;
                return false;
            }

            foreach (var symbol in production.Symbols)
            {
                if (symbol is null ||
                    symbol.Kind is not ("terminal" or "nonterminal" or "binding") ||
                    string.IsNullOrEmpty(symbol.Value) ||
                    symbol.Value.Length > 2048)
                {
                    error = "A grammar symbol has an unsupported kind or invalid value.";
                    pack = null;
                    return false;
                }
            }
        }

        if (!productions.ContainsKey(pack.StartSymbol))
        {
            error = "The grammar start symbol has no production.";
            pack = null;
            return false;
        }

        var bindings = new Dictionary<string, GenerationBinding>(StringComparer.Ordinal);
        foreach (var binding in pack.Bindings)
        {
            if (binding is null ||
                !IsIdentifier(binding.Name) ||
                binding.Source is not ("selected-operation" or "goal-argument") ||
                binding.Encoding is not ("none" or "string-literal") ||
                !bindings.TryAdd(binding.Name, binding))
            {
                error = "Every generation binding requires a unique name and a supported source and encoding.";
                pack = null;
                return false;
            }

            if (binding.Encoding == "string-literal" && binding.Source != "goal-argument")
            {
                error = "String-literal encoding can only be applied to the semantic goal argument.";
                pack = null;
                return false;
            }
        }

        foreach (var symbol in pack.Productions.SelectMany(production => production.Symbols))
        {
            if ((symbol.Kind == "nonterminal" && !productions.ContainsKey(symbol.Value)) ||
                (symbol.Kind == "binding" && !bindings.ContainsKey(symbol.Value)))
            {
                error = "The grammar references an undefined nonterminal or binding.";
                pack = null;
                return false;
            }
        }

        var grammarBindings = pack.Productions
            .SelectMany(production => production.Symbols)
            .Where(symbol => symbol.Kind == "binding")
            .Select(symbol => symbol.Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal);
        if (!grammarBindings.SequenceEqual(bindings.Keys.OrderBy(name => name, StringComparer.Ordinal)))
        {
            error = "Every declared generation binding must be used by the grammar, and every grammar binding must be declared.";
            pack = null;
            return false;
        }

        var operationIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var operation in pack.Operations)
        {
            if (operation is null ||
                !IsIdentifier(operation.Id) ||
                string.IsNullOrWhiteSpace(operation.Symbol) ||
                operation.Symbol.Length > 128 ||
                !IsIdentifier(operation.Effect) ||
                !IsIdentifier(operation.ArgumentKind) ||
                operation.Prerequisites.IsDefault ||
                operation.Prerequisites.Any(prerequisite => !IsCapabilityIdentifier(prerequisite)) ||
                !operationIds.Add(operation.Id))
            {
                error = "Every generation operation requires a unique identifier, symbol, effect, argument kind, and valid prerequisite list.";
                pack = null;
                return false;
            }
        }

        var escapeCharacters = new HashSet<string>(StringComparer.Ordinal);
        foreach (var escape in pack.StringLiteralFormat.Escapes)
        {
            if (escape is null ||
                escape.Character is null ||
                escape.Character.Length != 1 ||
                escape.Replacement is null ||
                !escapeCharacters.Add(escape.Character))
            {
                error = "String-literal escape rules must map unique single characters to replacement text.";
                pack = null;
                return false;
            }
        }

        return true;
    }

    public bool TryLoadEmbedded(
        out ImmutableDictionary<string, GenerationLanguagePack> packs,
        out string? error)
    {
        packs = ImmutableDictionary.Create<string, GenerationLanguagePack>(StringComparer.OrdinalIgnoreCase);
        error = null;

        var assembly = typeof(GenerationLanguagePackLoader).Assembly;
        var resources = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal) &&
                name.EndsWith(".json", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        if (resources.Length == 0)
        {
            error = "No embedded generation language packs were found.";
            return false;
        }

        var builder = ImmutableDictionary.CreateBuilder<string, GenerationLanguagePack>(StringComparer.OrdinalIgnoreCase);
        foreach (var resource in resources)
        {
            using var stream = assembly.GetManifestResourceStream(resource);
            if (stream is null)
            {
                error = $"The embedded generation language pack '{resource}' is missing.";
                return false;
            }

            using var reader = new StreamReader(stream);
            if (!TryLoad(reader.ReadToEnd(), out var pack, out error))
            {
                return false;
            }

            if (builder.ContainsKey(pack!.Language))
            {
                error = $"More than one embedded generation pack declares language '{pack.Language}'.";
                return false;
            }

            builder.Add(pack.Language, pack);
        }

        packs = builder.ToImmutable();
        return true;
    }

    private static bool IsCapabilityIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 120)
        {
            return false;
        }

        var segments = value.Split(':');
        return segments.Length is 1 or 2 && segments.All(IsIdentifier);
    }

    private static bool IsIdentifier(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 80 &&
        value.All(character =>
            char.IsAsciiLetterOrDigit(character) ||
            character is '-' or '_');
}
