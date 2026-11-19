using System.Collections.Immutable;
using System.Text.Json;

namespace Lattice.Core.Generation;

public static class SequenceKnowledgeBaseLoader
{
    private const int SupportedFormatVersion = 1;
    private const int MaximumJsonLength = 2_097_152;
    private const int MaximumRules = 10_000;
    private const int MaximumEncodings = 128;
    private const int MaximumArrayItems = 128;
    private const int MaximumContextItems = 64;
    private const int MaximumTokenLength = 4096;
    private const int MaximumIdentifierLength = 128;
    private const int MaximumDelimiterLength = 32;
    private const string ResourcePrefix = "Lattice.Core.SequenceKnowledge.";

    public static bool TryLoad(
        string json,
        out SequenceKnowledgeBase? knowledge,
        out string? error)
    {
        knowledge = null;
        error = null;

        if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumJsonLength)
        {
            error = "A non-empty sequence knowledge document within the supported size limit is required.";
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

            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !HasOnlyProperties(root, "formatVersion", "rules", "encodings") ||
                !TryGetInt32(root, "formatVersion", out var formatVersion) ||
                formatVersion != SupportedFormatVersion ||
                !TryGetArray(root, "rules", MaximumRules, required: true, out var rulesElement) ||
                rulesElement.GetArrayLength() == 0 ||
                !TryGetArray(root, "encodings", MaximumEncodings, required: false, out var encodingElements) ||
                !TryReadEncodings(encodingElements, out var encodings, out error))
            {
                error ??= "The sequence knowledge metadata, rule collection, or encodings are invalid or unsupported.";
                return false;
            }

            var rules = ImmutableArray.CreateBuilder<SequenceTokenRule>(rulesElement.GetArrayLength());
            var identifiers = new HashSet<string>(StringComparer.Ordinal);
            foreach (var element in rulesElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object ||
                    !HasOnlyProperties(
                        element,
                        "id",
                        "contextSuffix",
                        "requiredFacts",
                        "requiresCompletedFacts",
                        "nextToken",
                        "tokenBinding",
                        "satisfiesFacts",
                        "weight") ||
                    !TryGetString(element, "id", out var id) ||
                    !IsValidIdentifier(id) ||
                    !identifiers.Add(id) ||
                    !TryGetOptionalString(element, "nextToken", out var nextToken) ||
                    !TryGetTokenBinding(element, out var tokenBinding) ||
                    (nextToken is null) == (tokenBinding is null) ||
                    (nextToken is not null &&
                        (!IsValidToken(nextToken) ||
                         nextToken == SequenceKnowledgeBase.StartToken ||
                         nextToken == SequenceTokenModel.EndToken)) ||
                    !TryGetStringArray(element, "contextSuffix", out var contextSuffix) ||
                    !TryGetStringArray(element, "requiredFacts", out var requiredFacts) ||
                    !TryGetStringArray(element, "requiresCompletedFacts", out var requiresCompletedFacts) ||
                    !TryGetStringArray(element, "satisfiesFacts", out var satisfiesFacts) ||
                    !TryGetWeight(element, out var weight) ||
                    !HasUniqueIdentifiers(requiredFacts) ||
                    !HasUniqueIdentifiers(requiresCompletedFacts) ||
                    !HasUniqueIdentifiers(satisfiesFacts) ||
                    (tokenBinding is not null && !requiredFacts.Contains(tokenBinding.FactId, StringComparer.Ordinal)))
                {
                    error = "Every sequence rule requires a unique identifier, exactly one literal or bound token source, valid fact identifiers, context, and a finite optional weight.";
                    return false;
                }

                rules.Add(new SequenceTokenRule(
                    id,
                    contextSuffix,
                    requiredFacts,
                    requiresCompletedFacts,
                    nextToken,
                    satisfiesFacts,
                    weight)
                {
                    TokenBinding = tokenBinding
                });
            }

            knowledge = new SequenceKnowledgeBase(rules.ToImmutable(), encodings);
            return true;
        }
        catch (JsonException)
        {
            error = "The sequence knowledge document is not valid JSON.";
            return false;
        }
    }

    public static bool TryLoadEmbedded(
        out SequenceKnowledgeBase? knowledge,
        out string? error)
    {
        knowledge = null;
        error = null;

        var assembly = typeof(SequenceKnowledgeBaseLoader).Assembly;
        var resources = assembly.GetManifestResourceNames()
            .Where(name =>
                name.StartsWith(ResourcePrefix, StringComparison.Ordinal) &&
                name.EndsWith(".json", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        if (resources.Length == 0)
        {
            error = "No embedded sequence knowledge modules were found.";
            return false;
        }

        var rules = ImmutableArray.CreateBuilder<SequenceTokenRule>();
        var encodings = ImmutableArray.CreateBuilder<SequenceTokenEncoding>();
        var ruleIdentifiers = new HashSet<string>(StringComparer.Ordinal);
        var encodingIdentifiers = new HashSet<string>(StringComparer.Ordinal);

        foreach (var resource in resources)
        {
            using var stream = assembly.GetManifestResourceStream(resource);
            if (stream is null)
            {
                error = $"The embedded sequence knowledge module '{resource}' is missing.";
                return false;
            }

            using var reader = new StreamReader(stream);
            if (!TryLoad(reader.ReadToEnd(), out var module, out error))
            {
                return false;
            }

            foreach (var rule in module!.Rules)
            {
                if (!ruleIdentifiers.Add(rule.Id))
                {
                    error = $"More than one sequence knowledge module declares rule '{rule.Id}'.";
                    return false;
                }

                rules.Add(rule);
                if (rules.Count > MaximumRules)
                {
                    error = "The combined sequence knowledge modules contain too many rules.";
                    return false;
                }
            }

            foreach (var encoding in module.Encodings.IsDefault
                ? ImmutableArray<SequenceTokenEncoding>.Empty
                : module.Encodings)
            {
                if (!encodingIdentifiers.Add(encoding.Id))
                {
                    error = $"More than one sequence knowledge module declares encoding '{encoding.Id}'.";
                    return false;
                }

                encodings.Add(encoding);
                if (encodings.Count > MaximumEncodings)
                {
                    error = "The combined sequence knowledge modules contain too many token encodings.";
                    return false;
                }
            }
        }

        foreach (var rule in rules)
        {
            if (rule.TokenBinding?.Encoding is { } encoding &&
                !encodingIdentifiers.Contains(encoding))
            {
                error = $"The token binding in rule '{rule.Id}' references missing encoding '{encoding}'.";
                return false;
            }
        }

        knowledge = new SequenceKnowledgeBase(rules.ToImmutable(), encodings.ToImmutable());
        return true;
    }

    private static bool TryReadEncodings(
        JsonElement element,
        out ImmutableArray<SequenceTokenEncoding> encodings,
        out string? error)
    {
        encodings = ImmutableArray<SequenceTokenEncoding>.Empty;
        error = null;
        if (element.ValueKind == JsonValueKind.Undefined)
        {
            return true;
        }

        var builder = ImmutableArray.CreateBuilder<SequenceTokenEncoding>(element.GetArrayLength());
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !HasOnlyProperties(item, "id", "openingDelimiter", "closingDelimiter", "escapes") ||
                !TryGetString(item, "id", out var id) ||
                !IsValidIdentifier(id) ||
                !identifiers.Add(id) ||
                !TryGetString(item, "openingDelimiter", out var openingDelimiter) ||
                !IsValidDelimiter(openingDelimiter) ||
                !TryGetString(item, "closingDelimiter", out var closingDelimiter) ||
                !IsValidDelimiter(closingDelimiter) ||
                !TryGetArray(item, "escapes", MaximumArrayItems, required: true, out var escapeElements))
            {
                error = "Every token encoding requires a unique identifier, delimiters, and an escape-rule array.";
                return false;
            }

            var escapes = ImmutableArray.CreateBuilder<SequenceTokenEscape>(escapeElements.GetArrayLength());
            var characters = new HashSet<string>(StringComparer.Ordinal);
            foreach (var escape in escapeElements.EnumerateArray())
            {
                if (escape.ValueKind != JsonValueKind.Object ||
                    !HasOnlyProperties(escape, "character", "replacement") ||
                    !TryGetString(escape, "character", out var character) ||
                    character.Length != 1 ||
                    !TryGetString(escape, "replacement", out var replacement) ||
                    !characters.Add(character))
                {
                    error = $"Token encoding '{id}' contains an invalid or duplicate escape rule.";
                    return false;
                }

                escapes.Add(new SequenceTokenEscape(character, replacement));
            }

            builder.Add(new SequenceTokenEncoding(
                id,
                openingDelimiter,
                closingDelimiter,
                escapes.ToImmutable()));
        }

        encodings = builder.ToImmutable();
        return true;
    }

    private static bool TryGetTokenBinding(
        JsonElement element,
        out SequenceTokenBinding? binding)
    {
        binding = null;
        if (!element.TryGetProperty("tokenBinding", out var property))
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.Object ||
            !HasOnlyProperties(property, "factId", "valueId", "encoding") ||
            !TryGetString(property, "factId", out var factId) ||
            !IsValidIdentifier(factId) ||
            !TryGetString(property, "valueId", out var valueId) ||
            !IsValidIdentifier(valueId) ||
            !TryGetOptionalString(property, "encoding", out var encoding) ||
            (encoding is not null && !IsValidIdentifier(encoding)))
        {
            return false;
        }

        binding = new SequenceTokenBinding(factId, valueId, encoding);
        return true;
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

    private static bool TryGetArray(
        JsonElement element,
        string name,
        int maximumLength,
        bool required,
        out JsonElement value)
    {
        value = default;
        if (!element.TryGetProperty(name, out value))
        {
            return !required;
        }

        return value.ValueKind == JsonValueKind.Array &&
            value.GetArrayLength() <= maximumLength;
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

    private static bool TryGetOptionalString(
        JsonElement element,
        string name,
        out string? value)
    {
        value = null;
        if (!element.TryGetProperty(name, out var property))
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryGetStringArray(
        JsonElement element,
        string name,
        out ImmutableArray<string> values)
    {
        values = ImmutableArray<string>.Empty;
        if (!element.TryGetProperty(name, out var property))
        {
            return true;
        }

        var maximumLength = name == "contextSuffix" ? MaximumContextItems : MaximumArrayItems;
        if (property.ValueKind != JsonValueKind.Array ||
            property.GetArrayLength() > maximumLength)
        {
            return false;
        }

        var builder = ImmutableArray.CreateBuilder<string>(property.GetArrayLength());
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var value = item.GetString();
            if (string.IsNullOrWhiteSpace(value) ||
                (name == "contextSuffix" &&
                    (!IsValidToken(value) || value == SequenceTokenModel.EndToken)))
            {
                return false;
            }

            if (name != "contextSuffix" && !IsValidIdentifier(value))
            {
                return false;
            }

            builder.Add(value);
        }

        values = builder.ToImmutable();
        return true;
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

    private static bool HasUniqueIdentifiers(ImmutableArray<string> values) =>
        values.Distinct(StringComparer.Ordinal).Count() == values.Length;

    private static bool IsValidIdentifier(string value) =>
        value.Length is > 0 and <= MaximumIdentifierLength &&
        !string.IsNullOrWhiteSpace(value) &&
        value.All(character =>
            char.IsAsciiLetterOrDigit(character) ||
            character is '.' or '-' or '_' or ':');

    private static bool IsValidToken(string value) =>
        value.Length is > 0 and <= MaximumTokenLength;

    private static bool IsValidDelimiter(string value) =>
        value.Length is > 0 and <= MaximumDelimiterLength;
}
