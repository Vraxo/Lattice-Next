using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lattice.Core.Language;
using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

public sealed class KnowledgePackLoader
{
    private const int SupportedFormatVersion = 1;
    private const int MaximumClaims = 500;
    private const int MaximumAttributes = 500;
    private static readonly Regex PackIdPattern = new(
        "^[a-z0-9]+(?:-[a-z0-9]+)*$",
        RegexOptions.CultureInvariant);

    private readonly LceParser parser = new();

    public bool TryLoadAndAdd(
        string? json,
        KnowledgeBase knowledgeBase,
        out KnowledgePack? pack,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(knowledgeBase);

        pack = null;
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "The knowledge pack is empty.";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "A knowledge pack must be a JSON object.";
                return false;
            }

            var properties = root.EnumerateObject().ToArray();
            var propertyNames = properties.Select(property => property.Name).ToArray();

            if (propertyNames.Distinct(StringComparer.Ordinal).Count() != properties.Length ||
                propertyNames.Any(name => name is not ("formatVersion" or "id" or "name" or "claims" or "attributes")) ||
                properties.Length is < 4 or > 5)
            {
                error = "A version 1 pack must contain formatVersion, id, name, and claims, with optional attributes.";
                return false;
            }

            if (!root.TryGetProperty("formatVersion", out var versionElement) ||
                !versionElement.TryGetInt32(out var version))
            {
                error = "The pack formatVersion must be an integer.";
                return false;
            }

            if (version != SupportedFormatVersion)
            {
                error = $"Knowledge pack format version {version} is not supported. Supported version: {SupportedFormatVersion}.";
                return false;
            }

            if (!root.TryGetProperty("id", out var idElement) ||
                idElement.ValueKind != JsonValueKind.String)
            {
                error = "The pack id must be a string.";
                return false;
            }

            var id = idElement.GetString();

            if (string.IsNullOrWhiteSpace(id) ||
                id.Length > 64 ||
                !PackIdPattern.IsMatch(id))
            {
                error = "The pack id must contain lowercase letters, numbers, and single hyphens between segments.";
                return false;
            }

            if (!root.TryGetProperty("name", out var nameElement) ||
                nameElement.ValueKind != JsonValueKind.String)
            {
                error = "The pack name must be a string.";
                return false;
            }

            var name = nameElement.GetString();

            if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
            {
                error = "The pack name must contain between 1 and 120 characters.";
                return false;
            }

            if (!root.TryGetProperty("claims", out var claimsElement) ||
                claimsElement.ValueKind != JsonValueKind.Array)
            {
                error = "The pack claims must be an array of LCE assertions.";
                return false;
            }

            var claimElements = claimsElement.EnumerateArray().ToArray();

            if (claimElements.Length > MaximumClaims)
            {
                error = $"A pack can contain at most {MaximumClaims} claims.";
                return false;
            }

            var utterances = new List<SemanticUtterance>(claimElements.Length);
            var validationKnowledgeBase = new KnowledgeBase();

            foreach (var claimElement in claimElements)
            {
                if (claimElement.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(claimElement.GetString()))
                {
                    error = "Every pack claim must be a non-empty string.";
                    return false;
                }

                var claim = parser.ParseText(claimElement.GetString());

                if (!claim.IsSuccess ||
                    claim.Utterances.Length != 1 ||
                    claim.Utterances[0].Kind != UtteranceKind.Assertion)
                {
                    error = $"Invalid pack claim: {claim.Error ?? "Each claim must be exactly one supported assertion."}";
                    return false;
                }

                var utterance = claim.Utterances[0];

                if (!validationKnowledgeBase.TryAdd(
                    utterance,
                    out _,
                    out var validationError,
                    KnowledgeSourceKind.ImportedClaim,
                    id))
                {
                    error = $"Unsupported pack claim '{utterance.SourceText}': {validationError}";
                    return false;
                }

                utterances.Add(utterance);
            }

            var attributes = ImmutableArray<KnowledgeAttribute>.Empty;

            if (root.TryGetProperty("attributes", out var attributesElement) &&
                !TryParseAttributes(attributesElement, id!, out attributes, out error))
            {
                return false;
            }

            if (utterances.Count == 0 && attributes.IsDefaultOrEmpty)
            {
                error = "A pack must contain at least one claim or typed attribute.";
                return false;
            }

            var validationAttributes = new KnowledgeBase();

            foreach (var attribute in attributes)
            {
                if (!validationAttributes.TryAddAttribute(attribute, out error))
                {
                    return false;
                }
            }

            foreach (var utterance in utterances)
            {
                if (!knowledgeBase.TryAdd(
                    utterance,
                    out _,
                    out var addError,
                    KnowledgeSourceKind.ImportedClaim,
                    id))
                {
                    error = $"The validated pack could not be added: {addError}";
                    return false;
                }
            }

            foreach (var attribute in attributes)
            {
                if (!knowledgeBase.TryAddAttribute(attribute, out var attributeError))
                {
                    error = $"The validated pack attribute could not be added: {attributeError}";
                    return false;
                }
            }

            pack = new KnowledgePack(version, id, name, utterances.Count)
            {
                AttributeCount = attributes.Length
            };
            return true;
        }
        catch (JsonException)
        {
            error = "The knowledge pack is not valid JSON.";
            return false;
        }
    }
    private static bool TryParseAttributes(
        JsonElement element,
        string packId,
        out ImmutableArray<KnowledgeAttribute> attributes,
        out string? error)
    {
        attributes = ImmutableArray<KnowledgeAttribute>.Empty;
        error = null;

        if (element.ValueKind != JsonValueKind.Array)
        {
            error = "Pack attributes must be an array.";
            return false;
        }

        var items = element.EnumerateArray().ToArray();

        if (items.Length > MaximumAttributes)
        {
            error = $"A pack can contain at most {MaximumAttributes} typed attributes.";
            return false;
        }

        var parsed = ImmutableArray.CreateBuilder<KnowledgeAttribute>(items.Length);

        foreach (var item in items)
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !HasExactProperties(item, "subject", "name", "value", "sourceText") ||
                !TryGetString(item, "subject", out var subject) ||
                !TryGetString(item, "name", out var name) ||
                !TryGetString(item, "sourceText", out var sourceText) ||
                string.IsNullOrWhiteSpace(subject) ||
                string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(sourceText) ||
                !item.TryGetProperty("value", out var valueElement) ||
                !TryParseAttributeValue(valueElement, out var value))
            {
                error = "Every typed attribute must contain non-empty subject, name, and sourceText strings plus a supported typed value.";
                return false;
            }

            parsed.Add(new KnowledgeAttribute(
                new SemanticIdentifier(subject),
                new SemanticIdentifier(name),
                value!,
                sourceText,
                KnowledgeSourceKind.ImportedClaim,
                SourceId: packId));
        }

        attributes = parsed.ToImmutable();
        return true;
    }

    private static bool TryParseAttributeValue(
        JsonElement element,
        out ObjectiveConditionValue? value)
    {
        value = null;

        if (element.ValueKind != JsonValueKind.Object ||
            !TryGetString(element, "type", out var type))
        {
            return false;
        }

        switch (type)
        {
            case "money":
                if (!HasExactProperties(element, "type", "amount", "currencySymbol") ||
                    !element.TryGetProperty("amount", out var amountElement) ||
                    !amountElement.TryGetDecimal(out var amount) ||
                    !TryGetString(element, "currencySymbol", out var currencySymbol) ||
                    string.IsNullOrWhiteSpace(currencySymbol) ||
                    amount < 0m)
                {
                    return false;
                }

                value = new ObjectiveMoneyValue(amount, currencySymbol);
                return true;

            case "text":
                if (!HasExactProperties(element, "type", "value") ||
                    !TryGetString(element, "value", out var text) ||
                    string.IsNullOrWhiteSpace(text))
                {
                    return false;
                }

                value = new ObjectiveTextValue(text);
                return true;

            case "textSet":
                if (!HasExactProperties(element, "type", "values") ||
                    !element.TryGetProperty("values", out var valuesElement) ||
                    valuesElement.ValueKind != JsonValueKind.Array)
                {
                    return false;
                }

                var values = valuesElement.EnumerateArray().ToArray();

                if (values.Length == 0 ||
                    values.Any(item => item.ValueKind != JsonValueKind.String ||
                        string.IsNullOrWhiteSpace(item.GetString())))
                {
                    return false;
                }

                value = new ObjectiveTextSetValue(values
                    .Select(item => item.GetString()!)
                    .ToImmutableArray());
                return true;

            case "timeWindow":
                if (!HasExactProperties(element, "type", "start", "end") ||
                    !TryGetString(element, "start", out var startText) ||
                    !TryGetString(element, "end", out var endText) ||
                    !TimeOnly.TryParse(startText, CultureInfo.InvariantCulture, out var start) ||
                    !TimeOnly.TryParse(endText, CultureInfo.InvariantCulture, out var end) ||
                    start >= end)
                {
                    return false;
                }

                value = new ObjectiveTimeWindowValue(start, end);
                return true;

            case "duration":
                if (!HasExactProperties(element, "type", "minutes") ||
                    !element.TryGetProperty("minutes", out var minutesElement) ||
                    !minutesElement.TryGetInt32(out var minutes) ||
                    minutes < 0)
                {
                    return false;
                }

                value = new ObjectiveDurationValue(minutes);
                return true;

            case "timeSet":
                if (!HasExactProperties(element, "type", "times") ||
                    !element.TryGetProperty("times", out var timesElement) ||
                    timesElement.ValueKind != JsonValueKind.Array)
                {
                    return false;
                }

                var timeItems = timesElement.EnumerateArray().ToArray();

                if (timeItems.Length == 0 ||
                    timeItems.Any(item => item.ValueKind != JsonValueKind.String ||
                        !TimeOnly.TryParse(item.GetString(), CultureInfo.InvariantCulture, out _)))
                {
                    return false;
                }

                value = new ObjectiveTimeSetValue(timeItems
                    .Select(item => TimeOnly.Parse(item.GetString()!, CultureInfo.InvariantCulture))
                    .ToImmutableArray());
                return true;

            case "temporalPlan":
                if (!HasExactProperties(element, "type", "stages") ||
                    !element.TryGetProperty("stages", out var stagesElement) ||
                    stagesElement.ValueKind != JsonValueKind.Array)
                {
                    return false;
                }

                var stageItems = stagesElement.EnumerateArray().ToArray();

                if (stageItems.Length is 0 or > 64)
                {
                    return false;
                }

                var stages = ImmutableArray.CreateBuilder<ObjectiveTemporalStage>(stageItems.Length);

                foreach (var stageElement in stageItems)
                {
                    if (stageElement.ValueKind != JsonValueKind.Object)
                    {
                        return false;
                    }

                    var stageProperties = stageElement.EnumerateObject().ToArray();
                    var stageNames = stageProperties.Select(property => property.Name).ToArray();

                    if (stageNames.Distinct(StringComparer.Ordinal).Count() != stageNames.Length ||
                        !stageNames.Contains("durationAttribute", StringComparer.Ordinal) ||
                        stageNames.Any(name => name is not ("durationAttribute" or "startTimesAttribute" or "availabilityWindowAttribute")) ||
                        !TryGetString(stageElement, "durationAttribute", out var durationAttribute) ||
                        string.IsNullOrWhiteSpace(durationAttribute))
                    {
                        return false;
                    }

                    string? startTimesAttribute = null;
                    if (stageElement.TryGetProperty("startTimesAttribute", out _) &&
                        (!TryGetString(stageElement, "startTimesAttribute", out var startTimes) ||
                            string.IsNullOrWhiteSpace(startTimes)))
                    {
                        return false;
                    }
                    else if (stageElement.TryGetProperty("startTimesAttribute", out _))
                    {
                        TryGetString(stageElement, "startTimesAttribute", out startTimesAttribute!);
                    }

                    string? availabilityWindowAttribute = null;
                    if (stageElement.TryGetProperty("availabilityWindowAttribute", out _) &&
                        (!TryGetString(stageElement, "availabilityWindowAttribute", out var availabilityWindow) ||
                            string.IsNullOrWhiteSpace(availabilityWindow)))
                    {
                        return false;
                    }
                    else if (stageElement.TryGetProperty("availabilityWindowAttribute", out _))
                    {
                        TryGetString(stageElement, "availabilityWindowAttribute", out availabilityWindowAttribute!);
                    }

                    stages.Add(new ObjectiveTemporalStage(
                        durationAttribute,
                        startTimesAttribute,
                        availabilityWindowAttribute));
                }

                value = new ObjectiveTemporalPlanValue(stages.ToImmutable());
                return true;

            default:
                return false;
        }
    }

    private static bool TryGetString(
        JsonElement element,
        string propertyName,
        out string value)
    {
        value = string.Empty;

        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return true;
    }

    private static bool HasExactProperties(JsonElement element, params string[] expected)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var properties = element.EnumerateObject().ToArray();
        var names = properties.Select(property => property.Name).ToArray();

        return names.Length == expected.Length &&
            names.Distinct(StringComparer.Ordinal).Count() == names.Length &&
            names.OrderBy(name => name, StringComparer.Ordinal)
                .SequenceEqual(expected.OrderBy(name => name, StringComparer.Ordinal), StringComparer.Ordinal);
    }

}
