using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

internal static class KnowledgePackAttributeParser
{
    private const int MaximumAttributes = 500;

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
            JsonElement summaryElement = default;
            JsonElement candidateElement = default;
            JsonElement requiresFreshnessElement = default;
            JsonElement freshUntilElement = default;
            var hasSummary = item.ValueKind == JsonValueKind.Object &&
                item.TryGetProperty("summary", out summaryElement);
            var hasCandidate = item.ValueKind == JsonValueKind.Object &&
                item.TryGetProperty("candidate", out candidateElement);
            var hasRequiresFreshness = item.ValueKind == JsonValueKind.Object &&
                item.TryGetProperty("requiresFreshness", out requiresFreshnessElement);
            var hasFreshUntil = item.ValueKind == JsonValueKind.Object &&
                item.TryGetProperty("freshUntil", out freshUntilElement);
            DateTimeOffset? freshUntil = null;

            if (hasFreshUntil)
            {
                if (freshUntilElement.ValueKind != JsonValueKind.String ||
                    !DateTimeOffset.TryParse(
                        freshUntilElement.GetString(),
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                        out var parsedFreshUntil))
                {
                    error = "A typed attribute freshUntil value must be a valid timestamp.";
                    return false;
                }

                freshUntil = parsedFreshUntil;
            }

            if (item.ValueKind != JsonValueKind.Object ||
                !HasOnlyProperties(item, "subject", "name", "value", "sourceText", "summary", "candidate", "requiresFreshness", "freshUntil") ||
                (hasSummary && summaryElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) ||
                (hasCandidate && candidateElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) ||
                (hasRequiresFreshness && requiresFreshnessElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) ||
                !TryGetString(item, "subject", out var subject) ||
                !TryGetString(item, "name", out var name) ||
                !TryGetString(item, "sourceText", out var sourceText) ||
                string.IsNullOrWhiteSpace(subject) ||
                string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(sourceText) ||
                !item.TryGetProperty("value", out var valueElement) ||
                !TryParseAttributeValue(valueElement, out var value))
            {
                error = "Every typed attribute must contain non-empty subject, name, and sourceText strings plus a supported typed value; optional summary, requiresFreshness, and freshUntil metadata must be valid.";
                return false;
            }

            parsed.Add(new KnowledgeAttribute(
                new SemanticIdentifier(subject),
                new SemanticIdentifier(name),
                value!,
                sourceText,
                KnowledgeSourceKind.ImportedClaim,
                SourceId: packId)
            {
                IncludeInSummary = hasSummary && summaryElement.GetBoolean(),
                IsObjectiveCandidate = !hasCandidate || candidateElement.GetBoolean(),
                RequiresFreshness = (hasRequiresFreshness && requiresFreshnessElement.GetBoolean()) || hasFreshUntil,
                FreshUntil = freshUntil
            });
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
                var hasExcludedValues = element.TryGetProperty("excludedValues", out var excludedValuesElement);

                if ((!HasExactProperties(element, "type", "values") &&
                        !HasExactProperties(element, "type", "values", "excludedValues")) ||
                    !element.TryGetProperty("values", out var valuesElement) ||
                    valuesElement.ValueKind != JsonValueKind.Array ||
                    (hasExcludedValues && excludedValuesElement.ValueKind != JsonValueKind.Array))
                {
                    return false;
                }

                var values = valuesElement.EnumerateArray().ToArray();
                var excludedValues = hasExcludedValues
                    ? excludedValuesElement.EnumerateArray().ToArray()
                    : [];

                if ((values.Length == 0 && excludedValues.Length == 0) ||
                    values.Any(item => item.ValueKind != JsonValueKind.String ||
                        string.IsNullOrWhiteSpace(item.GetString())) ||
                    excludedValues.Any(item => item.ValueKind != JsonValueKind.String ||
                        string.IsNullOrWhiteSpace(item.GetString())))
                {
                    return false;
                }

                value = new ObjectiveTextSetValue(values
                    .Select(item => item.GetString()!)
                    .ToImmutableArray())
                {
                    ExcludedValues = excludedValues
                        .Select(item => item.GetString()!)
                        .ToImmutableArray()
                };
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
                    if (stageElement.TryGetProperty("startTimesAttribute", out var startTimesElement))
                    {
                        if (startTimesElement.ValueKind != JsonValueKind.String ||
                            string.IsNullOrWhiteSpace(startTimesElement.GetString()))
                        {
                            return false;
                        }

                        startTimesAttribute = startTimesElement.GetString();
                    }

                    string? availabilityWindowAttribute = null;
                    if (stageElement.TryGetProperty("availabilityWindowAttribute", out var availabilityWindowElement))
                    {
                        if (availabilityWindowElement.ValueKind != JsonValueKind.String ||
                            string.IsNullOrWhiteSpace(availabilityWindowElement.GetString()))
                        {
                            return false;
                        }

                        availabilityWindowAttribute = availabilityWindowElement.GetString();
                    }

                    stages.Add(new ObjectiveTemporalStage(
                        durationAttribute,
                        startTimesAttribute,
                        availabilityWindowAttribute));
                }

                value = new ObjectiveTemporalPlanValue(stages.ToImmutable());
                return true;

            case "sum":
                if (!HasExactProperties(element, "type", "attributes") ||
                    !element.TryGetProperty("attributes", out var componentElements) ||
                    componentElements.ValueKind != JsonValueKind.Array)
                {
                    return false;
                }

                var componentNames = componentElements.EnumerateArray().ToArray();

                if (componentNames.Length is < 2 or > 64 ||
                    componentNames.Any(item => item.ValueKind != JsonValueKind.String ||
                        string.IsNullOrWhiteSpace(item.GetString())))
                {
                    return false;
                }

                var componentAttributes = componentNames
                    .Select(item => item.GetString()!)
                    .ToImmutableArray();

                if (componentAttributes.Distinct(StringComparer.OrdinalIgnoreCase).Count() != componentAttributes.Length)
                {
                    return false;
                }

                value = new ObjectiveSumValue(componentAttributes);
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

    private static bool HasOnlyProperties(JsonElement element, params string[] allowed)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var names = element.EnumerateObject().Select(property => property.Name).ToArray();
        return names.Distinct(StringComparer.Ordinal).Count() == names.Length &&
            names.All(name => allowed.Contains(name, StringComparer.Ordinal));
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
