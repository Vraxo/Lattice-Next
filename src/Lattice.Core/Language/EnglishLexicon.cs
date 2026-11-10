using System.Collections.Frozen;
using System.Text;
using System.Text.Json;

namespace Lattice.Core.Language;

internal static class EnglishLexicon
{
    private const string ResourceName = "Lattice.Core.Language.english-lexicon.json";
    private static readonly Lazy<EnglishLexiconData> Data = new(LoadEmbedded);

    internal static FrozenSet<string> ReservedTerms => Data.Value.ReservedTerms;
    internal static FrozenSet<string> NamedIndividualPropertyAdjectives => Data.Value.NamedIndividualPropertyAdjectives;
    internal static FrozenDictionary<string, string> ExplicitPluralNounForms => Data.Value.ExplicitPluralNounForms;
    internal static FrozenSet<string> InvariantNouns => Data.Value.InvariantNouns;
    internal static FrozenSet<string> SingularNounsEndingInS => Data.Value.SingularNounsEndingInS;
    internal static FrozenDictionary<string, string> IrregularThirdPersonVerbs => Data.Value.IrregularThirdPersonVerbs;
    internal static FrozenSet<string> BaseRelationVerbs => Data.Value.BaseRelationVerbs;
    internal static FrozenDictionary<string, string> PastParticiples => Data.Value.PastParticiples;

    internal static bool TryLoad(
        string json,
        out EnglishLexiconData? lexicon,
        out string error)
    {
        lexicon = null;
        error = string.Empty;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "The lexicon root must be a JSON object.";
                return false;
            }

            if (!HasExpectedProperties(root, out error) ||
                !HasSupportedSchemaVersion(root))
            {
                if (error.Length == 0)
                {
                    error = "The lexicon schema-version must be 2.";
                }

                return false;
            }

            if (!TryReadStringSet(root, "reserved-terms", out var reservedTerms, out error) ||
                !TryReadStringSet(root, "named-individual-property-adjectives", out var namedIndividualPropertyAdjectives, out error) ||
                !TryReadStringMap(root, "irregular-plural-nouns", out var irregularPluralNouns, out error) ||
                !TryReadStringSet(root, "invariant-nouns", out var invariantNouns, out error) ||
                !TryReadStringSet(root, "singular-nouns-ending-in-s", out var singularNounsEndingInS, out error) ||
                !TryReadStringMap(root, "irregular-third-person-verbs", out var irregularThirdPersonVerbs, out error) ||
                !TryReadStringSet(root, "base-relation-verbs", out var baseRelationVerbs, out error) ||
                !TryReadStringMap(root, "past-participles", out var pastParticiples, out error))
            {
                return false;
            }

            if (reservedTerms.Count == 0 ||
                irregularPluralNouns.Count == 0 ||
                invariantNouns.Count == 0 ||
                singularNounsEndingInS.Count == 0 ||
                irregularThirdPersonVerbs.Count == 0 ||
                baseRelationVerbs.Count == 0 ||
                pastParticiples.Count == 0 ||
                namedIndividualPropertyAdjectives.Count == 0)
            {
                error = "Every lexicon collection must contain at least one entry.";
                return false;
            }

            if (irregularPluralNouns.Values.Any(value =>
                    value.EndsWith("s", StringComparison.Ordinal) &&
                    !singularNounsEndingInS.Contains(value)))
            {
                error = "Irregular plural singular forms ending in 's' must be listed in singular-nouns-ending-in-s.";
                return false;
            }

            if (irregularPluralNouns.Keys.Any(invariantNouns.Contains) ||
                irregularPluralNouns.Keys.Any(singularNounsEndingInS.Contains) ||
                invariantNouns.Any(singularNounsEndingInS.Contains))
            {
                error = "Noun classifications overlap; plural, invariant, and singular-ending-in-s entries must be disjoint.";
                return false;
            }

            if (irregularThirdPersonVerbs.Any(entry =>
                    !baseRelationVerbs.Contains(entry.Key) ||
                    !entry.Value.EndsWith("s", StringComparison.Ordinal)))
            {
                error = "Every irregular third-person verb must map a supported base relation verb to a third-person form ending in 's'.";
                return false;
            }

            if (pastParticiples.Any(entry => !baseRelationVerbs.Contains(entry.Value)))
            {
                error = "Every past participle must map to a verb in base-relation-verbs.";
                return false;
            }

            lexicon = new EnglishLexiconData(
                reservedTerms.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                namedIndividualPropertyAdjectives.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                irregularPluralNouns.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
                invariantNouns.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                singularNounsEndingInS.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                irregularThirdPersonVerbs.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
                baseRelationVerbs.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                pastParticiples.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase));
            return true;
        }
        catch (JsonException exception)
        {
            error = $"The lexicon JSON is invalid: {exception.Message}";
            return false;
        }
    }

    private static EnglishLexiconData LoadEmbedded()
    {
        using var stream = typeof(EnglishLexicon).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded English lexicon resource '{ResourceName}' was not found.");
        using var reader = new StreamReader(stream, Encoding.UTF8);

        if (!TryLoad(reader.ReadToEnd(), out var lexicon, out var error))
        {
            throw new InvalidOperationException($"The embedded English lexicon is invalid: {error}");
        }

        return lexicon!;
    }

    private static bool HasExpectedProperties(JsonElement root, out string error)
    {
        var expected = new HashSet<string>(StringComparer.Ordinal)
        {
            "schema-version",
            "reserved-terms",
            "named-individual-property-adjectives",
            "irregular-plural-nouns",
            "invariant-nouns",
            "singular-nouns-ending-in-s",
            "irregular-third-person-verbs",
            "base-relation-verbs",
            "past-participles"
        };
        var actual = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in root.EnumerateObject())
        {
            if (!actual.Add(property.Name))
            {
                error = $"The lexicon contains duplicate property '{property.Name}'.";
                return false;
            }

            if (!expected.Contains(property.Name))
            {
                error = $"The lexicon contains unknown property '{property.Name}'.";
                return false;
            }
        }

        if (!actual.SetEquals(expected))
        {
            error = "The lexicon is missing one or more required properties.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool HasSupportedSchemaVersion(JsonElement root) =>
        root.TryGetProperty("schema-version", out var version) &&
        version.ValueKind == JsonValueKind.Number &&
        version.TryGetInt32(out var value) &&
        value == 2;

    private static bool TryReadStringSet(
        JsonElement root,
        string propertyName,
        out HashSet<string> values,
        out string error)
    {
        values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        error = string.Empty;

        if (!root.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Array)
        {
            error = $"The lexicon property '{propertyName}' must be an array.";
            return false;
        }

        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String ||
                !IsLexicalToken(item.GetString()))
            {
                error = $"The lexicon array '{propertyName}' contains an invalid token.";
                return false;
            }

            var value = item.GetString()!;
            if (!values.Add(value))
            {
                error = $"The lexicon array '{propertyName}' contains duplicate token '{value}'.";
                return false;
            }
        }

        return true;
    }

    private static bool TryReadStringMap(
        JsonElement root,
        string propertyName,
        out Dictionary<string, string> values,
        out string error)
    {
        values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        error = string.Empty;

        if (!root.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Object)
        {
            error = $"The lexicon property '{propertyName}' must be an object.";
            return false;
        }

        foreach (var entry in property.EnumerateObject())
        {
            if (!IsLexicalToken(entry.Name) ||
                entry.Value.ValueKind != JsonValueKind.String ||
                !IsLexicalToken(entry.Value.GetString()))
            {
                error = $"The lexicon object '{propertyName}' contains an invalid entry.";
                return false;
            }

            if (!values.TryAdd(entry.Name, entry.Value.GetString()!))
            {
                error = $"The lexicon object '{propertyName}' contains duplicate key '{entry.Name}'.";
                return false;
            }
        }

        return true;
    }

    private static bool IsLexicalToken(string? value) =>
        !string.IsNullOrEmpty(value) &&
        value[0] is >= 'a' and <= 'z' &&
        value.All(character =>
            character is >= 'a' and <= 'z' or
                >= '0' and <= '9' or '-');
}
