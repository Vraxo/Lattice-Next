using System.Collections.Immutable;
using System.Text.Json;

namespace Lattice.Core.Generation;

public static class SequenceKnowledgeBaseLoader
{
    private const int SupportedFormatVersion = 1;
    private const int MaximumJsonLength = 2_097_152;
    private const int MaximumRules = 10_000;
    private const int MaximumArrayItems = 128;
    private const int MaximumTokenLength = 4096;
    private const int MaximumIdentifierLength = 128;
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
                !HasOnlyProperties(root, "formatVersion", "rules") ||
                !TryGetInt32(root, "formatVersion", out var formatVersion) ||
                formatVersion != SupportedFormatVersion ||
                !root.TryGetProperty("rules", out var rulesElement) ||
                rulesElement.ValueKind != JsonValueKind.Array ||
                rulesElement.GetArrayLength() is 0 or > MaximumRules)
            {
                error = "The sequence knowledge metadata or rule collection is invalid or unsupported.";
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
                        "satisfiesFacts",
                        "weight") ||
                    !TryGetString(element, "id", out var id) ||
                    !IsValidIdentifier(id) ||
                    !identifiers.Add(id) ||
                    !TryGetString(element, "nextToken", out var nextToken) ||
                    !IsValidToken(nextToken) ||
                    nextToken == SequenceKnowledgeBase.StartToken ||
                    nextToken == SequenceTokenModel.EndToken ||
                    !TryGetStringArray(element, "contextSuffix", out var contextSuffix) ||
                    !TryGetStringArray(element, "requiredFacts", out var requiredFacts) ||
                    !TryGetStringArray(element, "requiresCompletedFacts", out var requiresCompletedFacts) ||
                    !TryGetStringArray(element, "satisfiesFacts", out var satisfiesFacts) ||
                    !TryGetWeight(element, out var weight) ||
                    !HasUniqueIdentifiers(requiredFacts) ||
                    !HasUniqueIdentifiers(requiresCompletedFacts) ||
                    !HasUniqueIdentifiers(satisfiesFacts))
                {
                    error = "Every sequence rule requires a unique identifier, a next token, valid fact identifiers, context, and a finite optional weight.";
                    return false;
                }

                rules.Add(new SequenceTokenRule(
                    id,
                    contextSuffix,
                    requiredFacts,
                    requiresCompletedFacts,
                    nextToken,
                    satisfiesFacts,
                    weight));
            }

            knowledge = new SequenceKnowledgeBase(rules.ToImmutable());
            return true;
        }
        catch (JsonException)
        {
            error = "The sequence knowledge document is not valid JSON.";
            return false;
        }
    }

    public bool TryLoadEmbedded(
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
            .OrderBy(name, StringComparer.Ordinal)
            .ToArray();

        if (resources.Length == 0)
        {
            error = "No embedded sequence knowledge modules were found.";
            return false;
        }

        var rules = ImmutableArray.CreateBuilder<SequenceTokenRule>();
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
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
                if (!identifiers.Add(rule.Id))
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
        }

        knowledge = new SequenceKnowledgeBase(rules.ToImmutable());
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

        if (property.ValueKind != JsonValueKind.Array ||
            property.GetArrayLength() > (name == "contextSuffix" ? 64 : MaximumArrayItems))
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
}
