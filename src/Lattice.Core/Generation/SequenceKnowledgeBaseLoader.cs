using System.Collections.Immutable;
using System.Text.Json;

namespace Lattice.Core.Generation;

public static class SequenceKnowledgeBaseLoader
{
    private const int SupportedFormatVersion = 1;
    private const int MaximumJsonLength = 2_097_152;
    private const int MaximumInputRules = 4096;
    private const int MaximumValidations = 4096;
    private const int MaximumArrayItems = 128;
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
                !HasOnlyProperties(root, "formatVersion", "inputRules", "validations") ||
                !TryGetInt32(root, "formatVersion", out var formatVersion) ||
                formatVersion != SupportedFormatVersion ||
                !TryGetArray(root, "inputRules", MaximumInputRules, required: true, out var inputRuleElements) ||
                inputRuleElements.GetArrayLength() == 0 ||
                !TryReadInputRules(inputRuleElements, out var inputRules, out error) ||
                !TryGetArray(root, "validations", MaximumValidations, required: false, out var validationElements) ||
                !TryReadInputValidations(validationElements, out var validations, out error))
            {
                error ??= "The sequence knowledge metadata or input rule collection is invalid or unsupported.";
                return false;
            }

            knowledge = new SequenceKnowledgeBase(inputRules, validations);
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

        var inputRules = ImmutableArray.CreateBuilder<SequenceInputFactRule>();
        var validations = ImmutableArray.CreateBuilder<SequenceInputFactValidation>();
        var inputRuleIdentifiers = new HashSet<string>(StringComparer.Ordinal);
        var validationIdentifiers = new HashSet<string>(StringComparer.Ordinal);

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

            foreach (var inputRule in module.InputRules.IsDefault
                ? ImmutableArray<SequenceInputFactRule>.Empty
                : module.InputRules)
            {
                if (!inputRuleIdentifiers.Add(inputRule.Id))
                {
                    error = $"More than one sequence knowledge module declares input rule '{inputRule.Id}'.";
                    return false;
                }

                inputRules.Add(inputRule);
                if (inputRules.Count > MaximumInputRules)
                {
                    error = "The combined sequence knowledge modules contain too many input fact rules.";
                    return false;
                }
            }

            foreach (var validation in module.Validations.IsDefault
                ? ImmutableArray<SequenceInputFactValidation>.Empty
                : module.Validations)
            {
                if (!validationIdentifiers.Add(validation.Id))
                {
                    error = $"More than one sequence knowledge module declares input validation '{validation.Id}'.";
                    return false;
                }

                validations.Add(validation);
                if (validations.Count > MaximumValidations)
                {
                    error = "The combined sequence knowledge modules contain too many input validations.";
                    return false;
                }
            }
        }

        knowledge = new SequenceKnowledgeBase(
            inputRules.ToImmutable(),
            validations.ToImmutable());
        return true;
    }

    private static bool TryReadInputRules(
        JsonElement element,
        out ImmutableArray<SequenceInputFactRule> inputRules,
        out string? error)
    {
        inputRules = ImmutableArray<SequenceInputFactRule>.Empty;
        error = null;
        if (element.ValueKind == JsonValueKind.Undefined)
        {
            return true;
        }

        var builder = ImmutableArray.CreateBuilder<SequenceInputFactRule>(element.GetArrayLength());
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !HasOnlyProperties(item, "id", "matchTokens", "factId", "valueId", "literalValue", "captures", "searchAllSentences", "requiredAnySequences", "requiredAllSequences", "forbiddenAnySequences", "repeatMatches", "semanticExpressionKind") ||
                !TryGetString(item, "id", out var id) ||
                !IsValidIdentifier(id) ||
                !identifiers.Add(id) ||
                !TryGetStringArray(item, "matchTokens", out var matchTokens) ||
                matchTokens.IsDefaultOrEmpty ||
                matchTokens.Length > 16 ||
                !TryGetString(item, "factId", out var factId) ||
                !IsValidIdentifier(factId) ||
                !TryGetOptionalString(item, "valueId", out var valueId) ||
                (valueId is not null && !IsValidIdentifier(valueId)) ||
                !TryGetOptionalString(item, "literalValue", out var literalValue) ||
                (literalValue is not null && (valueId is null || literalValue.Length > 16_384)) ||
                !TryGetArray(item, "captures", 16, required: false, out var captureElements) ||
                !TryGetOptionalBoolean(item, "searchAllSentences", out var searchAllSentences) ||
                !TryGetOptionalBoolean(item, "repeatMatches", out var repeatMatches) ||
                !TryGetOptionalString(item, "semanticExpressionKind", out var semanticExpressionKind) ||
                !IsValidSemanticExpressionKind(semanticExpressionKind) ||
                (semanticExpressionKind is not null && valueId is null) ||
                !TryGetNestedStringArray(item, "requiredAnySequences", out var requiredAnySequences) ||
                requiredAnySequences.Length > 16 ||
                !TryGetNestedStringArray(item, "requiredAllSequences", out var requiredAllSequences) ||
                requiredAllSequences.Length > 16 ||
                !TryGetNestedStringArray(item, "forbiddenAnySequences", out var forbiddenAnySequences) ||
                forbiddenAnySequences.Length > 16 ||
                !TryReadInputCaptures(captureElements, out var captures, out error))
            {
                error = "Every input fact rule requires a unique identifier, ordered match tokens, a fact identifier, and an optional value identifier.";
                return false;
            }

            builder.Add(new SequenceInputFactRule(
                id,
                matchTokens,
                factId,
                valueId,
                literalValue,
                captures)
            {
                SearchAllSentences = searchAllSentences,
                RequiredAnySequences = requiredAnySequences,
                RequiredAllSequences = requiredAllSequences,
                ForbiddenAnySequences = forbiddenAnySequences,
                RepeatMatches = repeatMatches,
                SemanticExpressionKind = semanticExpressionKind
            });
        }

        inputRules = builder.ToImmutable();
        return true;
    }

    private static bool TryReadInputCaptures(
        JsonElement element,
        out ImmutableArray<SequenceInputFactCapture> captures,
        out string? error)
    {
        captures = ImmutableArray<SequenceInputFactCapture>.Empty;
        error = null;
        if (element.ValueKind == JsonValueKind.Undefined)
        {
            return true;
        }

        var builder = ImmutableArray.CreateBuilder<SequenceInputFactCapture>(element.GetArrayLength());
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !HasOnlyProperties(item, "factId", "valueId", "startAfterTokens", "startAfterSequences", "stopBeforeTokens", "stopBeforeSequences", "literalValue", "collectionDelimiter", "collectionFieldDelimiter", "collectionFieldIndex", "collectionTemplate", "semanticCollectionFormat", "trimPrefixTokens", "trimSuffixCharacters", "collectionDelimiters", "collectionItemSeparator", "preserveRawValue", "numericOffset", "semanticExpressionKind", "optionalExpression") ||
                !TryGetString(item, "factId", out var factId) ||
                !IsValidIdentifier(factId) ||
                !TryGetString(item, "valueId", out var valueId) ||
                !IsValidIdentifier(valueId) ||
                !identifiers.Add($"{factId}.{valueId}") ||
                !TryGetStringArray(item, "startAfterTokens", out var startAfterTokens) ||
                startAfterTokens.Length > 16 ||
                !TryGetNestedStringArray(item, "startAfterSequences", out var startAfterSequences) ||
                startAfterSequences.Length > 16 ||
                (startAfterTokens.Length > 0 && startAfterSequences.Length > 0) ||
                !TryGetStringArray(item, "stopBeforeTokens", out var stopBeforeTokens) ||
                stopBeforeTokens.Length > 16 ||
                !TryGetOptionalString(item, "literalValue", out var literalValue) ||
                (literalValue is not null && literalValue.Length > 16_384) ||
                !TryGetOptionalString(item, "collectionDelimiter", out var collectionDelimiter) ||
                (collectionDelimiter is not null && collectionDelimiter.Length > 32) ||
                !TryGetOptionalString(item, "collectionItemSeparator", out var collectionItemSeparator) ||
                (collectionItemSeparator is not null && collectionItemSeparator.Length > 32) ||
                !TryGetStringArray(item, "collectionDelimiters", out var collectionDelimiters) ||
                collectionDelimiters.Length > 16 ||
                (collectionDelimiter is not null && !collectionDelimiters.IsDefaultOrEmpty) ||
                !TryGetOptionalString(item, "collectionFieldDelimiter", out var collectionFieldDelimiter) ||
                (collectionFieldDelimiter is not null && collectionFieldDelimiter.Length > 32) ||
                !TryGetOptionalInt32(item, "collectionFieldIndex", out var collectionFieldIndex) ||
                !TryGetOptionalString(item, "collectionTemplate", out var collectionTemplate) ||
                (collectionTemplate is not null &&
                    (collectionTemplate.Length > 256 ||
                     !collectionTemplate.Contains("{0}", StringComparison.Ordinal) ||
                     !collectionTemplate.Contains("{1}", StringComparison.Ordinal))) ||
                !TryReadOptionalSemanticCollectionFormat(item, out var semanticCollectionFormat) ||
                !TryGetNestedStringArray(item, "stopBeforeSequences", out var stopBeforeSequences) ||
                !TryGetStringArray(item, "trimPrefixTokens", out var trimPrefixTokens) ||
                !TryGetOptionalString(item, "trimSuffixCharacters", out var trimSuffixCharacters) ||
                !TryGetOptionalBoolean(item, "preserveRawValue", out var preserveRawValue) ||
                !TryGetOptionalBoolean(item, "optionalExpression", out var optionalExpression) ||
                !TryGetOptionalString(item, "semanticExpressionKind", out var semanticExpressionKind) ||
                !IsValidSemanticExpressionKind(semanticExpressionKind) ||
                !TryGetOptionalInt32(item, "numericOffset", out var numericOffset) ||
                (semanticExpressionKind is not null && numericOffset is not null && numericOffset.Value != 0) ||
                (collectionItemSeparator is not null && collectionDelimiter is null && collectionDelimiters.IsDefaultOrEmpty) ||
                ((collectionFieldIndex is not null || collectionTemplate is not null) &&
                    ((collectionDelimiter is null && collectionDelimiters.IsDefaultOrEmpty) ||
                     collectionFieldDelimiter is null)) ||
                (collectionFieldDelimiter is not null &&
                    collectionFieldIndex is null && collectionTemplate is null) ||
                (collectionTemplate is not null && collectionFieldIndex is not null) ||
                (semanticCollectionFormat is not null &&
                    (!preserveRawValue ||
                     literalValue is not null ||
                     collectionDelimiter is not null ||
                     !collectionDelimiters.IsDefaultOrEmpty ||
                     collectionItemSeparator is not null ||
                     collectionFieldDelimiter is not null ||
                     collectionFieldIndex is not null ||
                     collectionTemplate is not null ||
                     semanticExpressionKind is not null ||
                     (numericOffset is not null && numericOffset.Value != 0))))
            {
                error = "Every input capture requires a unique fact/value pair and valid optional start, stop, and literal data.";
                return false;
            }

            builder.Add(new SequenceInputFactCapture(
                factId,
                valueId,
                startAfterTokens,
                stopBeforeTokens,
                literalValue)
            {
                CollectionDelimiter = collectionDelimiter,
                CollectionDelimiters = collectionDelimiters,
                StartAfterSequences = startAfterSequences,
                StopBeforeSequences = stopBeforeSequences,
                CollectionFieldDelimiter = collectionFieldDelimiter,
                CollectionFieldIndex = collectionFieldIndex,
                CollectionTemplate = collectionTemplate,
                SemanticCollectionFormat = semanticCollectionFormat,
                TrimPrefixTokens = trimPrefixTokens,
                TrimSuffixCharacters = trimSuffixCharacters ?? string.Empty,
                PreserveRawValue = preserveRawValue,
                SemanticExpressionKind = semanticExpressionKind,
                NumericOffset = numericOffset ?? 0,
                CollectionItemSeparator = collectionItemSeparator,
                OptionalExpression = optionalExpression
            });
        }

        captures = builder.ToImmutable();
        return true;
    }

    private static bool TryReadOptionalSemanticCollectionFormat(
        JsonElement capture,
        out SemanticCollectionInputFormat? format)
    {
        format = null;
        if (!capture.TryGetProperty("semanticCollectionFormat", out var property))
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.Object ||
            !HasOnlyProperties(property, "openingDelimiter", "closingDelimiter", "itemSeparator", "itemFormats") ||
            !TryGetString(property, "openingDelimiter", out var openingDelimiter) ||
            !IsValidDelimiter(openingDelimiter) ||
            string.IsNullOrWhiteSpace(openingDelimiter) ||
            !TryGetString(property, "closingDelimiter", out var closingDelimiter) ||
            !IsValidDelimiter(closingDelimiter) ||
            string.IsNullOrWhiteSpace(closingDelimiter) ||
            !TryGetString(property, "itemSeparator", out var itemSeparator) ||
            !IsValidDelimiter(itemSeparator) ||
            string.IsNullOrWhiteSpace(itemSeparator) ||
            !TryGetArray(property, "itemFormats", 16, required: true, out var itemElements) ||
            itemElements.GetArrayLength() == 0)
        {
            return false;
        }

        var itemFormats = ImmutableArray.CreateBuilder<SemanticCollectionInputItemFormat>(itemElements.GetArrayLength());
        var formatKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var itemElement in itemElements.EnumerateArray())
        {
            if (itemElement.ValueKind != JsonValueKind.Object ||
                !HasOnlyProperties(itemElement, "kind", "openingDelimiter", "closingDelimiter", "escapes") ||
                !TryGetString(itemElement, "kind", out var kindText) ||
                !TryGetOptionalString(itemElement, "openingDelimiter", out var itemOpeningDelimiter) ||
                !TryGetOptionalString(itemElement, "closingDelimiter", out var itemClosingDelimiter) ||
                !TryGetArray(itemElement, "escapes", 16, required: true, out var escapeElements))
            {
                return false;
            }

            var kind = kindText switch
            {
                "number" => SemanticCollectionItemKind.Number,
                "text" => SemanticCollectionItemKind.Text,
                _ => (SemanticCollectionItemKind?)null
            };
            var itemOpen = itemOpeningDelimiter ?? string.Empty;
            var itemClose = itemClosingDelimiter ?? string.Empty;
            if (kind is null ||
                (kind == SemanticCollectionItemKind.Number &&
                    (itemOpen.Length != 0 || itemClose.Length != 0 || escapeElements.GetArrayLength() != 0)) ||
                (kind == SemanticCollectionItemKind.Text &&
                    (itemOpen.Length == 0 || itemClose.Length == 0 ||
                     itemOpen.Length > MaximumDelimiterLength || itemClose.Length > MaximumDelimiterLength)) ||
                !formatKeys.Add($"{kindText}\0{itemOpen}\0{itemClose}"))
            {
                return false;
            }

            var escapes = ImmutableArray.CreateBuilder<SemanticCollectionInputEscape>(escapeElements.GetArrayLength());
            var escapeSequences = new HashSet<string>(StringComparer.Ordinal);
            foreach (var escapeElement in escapeElements.EnumerateArray())
            {
                if (escapeElement.ValueKind != JsonValueKind.Object ||
                    !HasOnlyProperties(escapeElement, "sequence", "value") ||
                    !TryGetString(escapeElement, "sequence", out var sequence) ||
                    sequence.Length > 16 ||
                    !TryGetString(escapeElement, "value", out var value) ||
                    value.Length > 16 ||
                    !escapeSequences.Add(sequence))
                {
                    return false;
                }

                escapes.Add(new SemanticCollectionInputEscape(sequence, value));
            }

            if (kind == SemanticCollectionItemKind.Text &&
                escapes.Any(escape => escape.Sequence.Length == 0))
            {
                return false;
            }

            itemFormats.Add(new SemanticCollectionInputItemFormat(
                kind.Value,
                itemOpen,
                itemClose,
                escapes.ToImmutable()));
        }

        format = new SemanticCollectionInputFormat(
            openingDelimiter,
            closingDelimiter,
            itemSeparator,
            itemFormats.ToImmutable());
        return true;
    }

    private static bool TryReadInputValidations(
        JsonElement element,
        out ImmutableArray<SequenceInputFactValidation> validations,
        out string? error)
    {
        validations = ImmutableArray<SequenceInputFactValidation>.Empty;
        error = null;
        if (element.ValueKind == JsonValueKind.Undefined)
        {
            return true;
        }

        var builder = ImmutableArray.CreateBuilder<SequenceInputFactValidation>(element.GetArrayLength());
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        var supportedKinds = new HashSet<string>(StringComparer.Ordinal)
        {
            "identifierValue",
            "identifierCollection",
            "uniqueCollection",
            "disjointCollections",
            "arithmeticCollectionReferences",
            "arithmeticValue",
            "arithmeticValueReferences",
            "comparisonValueReferences",
            "arithmeticExpressionReferences",
            "booleanExpressionCollectionReferences",
            "valueExpressionReferences",
            "valueExpressionCollectionReferences",
            "matchingCollections",
            "orderedArithmeticFunctionExpressions",
            "matchingLastNumericFunctionCall",
            "matchingFunctionCall",
            "matchingNumericFunctionCall",
            "semanticCollection",
            "integerValue",
            "ascendingIntegerRange"
        };

        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !HasOnlyProperties(item, "id", "kind", "factId", "valueId", "relatedFactId", "relatedValueId", "additionalFactId", "additionalValueId", "appliesWhenFactId") ||
                !TryGetString(item, "id", out var id) ||
                !IsValidIdentifier(id) ||
                !identifiers.Add(id) ||
                !TryGetString(item, "kind", out var kind) ||
                !supportedKinds.Contains(kind) ||
                !TryGetString(item, "factId", out var factId) ||
                !IsValidIdentifier(factId) ||
                !TryGetString(item, "valueId", out var valueId) ||
                !IsValidIdentifier(valueId) ||
                !TryGetOptionalString(item, "relatedFactId", out var relatedFactId) ||
                (relatedFactId is not null && !IsValidIdentifier(relatedFactId)) ||
                !TryGetOptionalString(item, "relatedValueId", out var relatedValueId) ||
                (relatedValueId is not null && !IsValidIdentifier(relatedValueId)) ||
                !TryGetOptionalString(item, "additionalFactId", out var additionalFactId) ||
                (additionalFactId is not null && !IsValidIdentifier(additionalFactId)) ||
                !TryGetOptionalString(item, "additionalValueId", out var additionalValueId) ||
                (additionalValueId is not null && !IsValidIdentifier(additionalValueId)) ||
                !TryGetOptionalString(item, "appliesWhenFactId", out var appliesWhenFactId) ||
                (appliesWhenFactId is not null && !IsValidIdentifier(appliesWhenFactId)))
            {
                error = "Every input validation requires a unique identifier, a supported constraint kind, and valid fact/value references.";
                return false;
            }

            builder.Add(new SequenceInputFactValidation(
                id,
                kind,
                factId,
                valueId,
                relatedFactId,
                relatedValueId,
                additionalFactId,
                additionalValueId)
            {
                AppliesWhenFactId = appliesWhenFactId
            });
        }

        validations = builder.ToImmutable();
        return true;
    }

    private static bool TryGetNestedStringArray(
        JsonElement element,
        string name,
        out ImmutableArray<ImmutableArray<string>> values)
    {
        values = ImmutableArray<ImmutableArray<string>>.Empty;
        if (!element.TryGetProperty(name, out var property))
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.Array || property.GetArrayLength() > 16)
        {
            return false;
        }

        var builder = ImmutableArray.CreateBuilder<ImmutableArray<string>>(property.GetArrayLength());
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Array || item.GetArrayLength() is 0 or > 16)
            {
                return false;
            }

            var sequence = ImmutableArray.CreateBuilder<string>(item.GetArrayLength());
            foreach (var token in item.EnumerateArray())
            {
                if (token.ValueKind != JsonValueKind.String ||
                    !IsValidIdentifier(token.GetString() ?? string.Empty))
                {
                    return false;
                }

                sequence.Add(token.GetString()!);
            }

            builder.Add(sequence.ToImmutable());
        }

        values = builder.ToImmutable();
        return true;
    }

    private static bool TryGetOptionalInt32(
        JsonElement element,
        string name,
        out int? value)
    {
        value = null;
        if (!element.TryGetProperty(name, out var property))
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.Number ||
            !property.TryGetInt32(out var parsed) ||
            parsed is < 0 or > 16)
        {
            return false;
        }

        value = parsed;
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

    private static bool TryGetOptionalBoolean(JsonElement element, string name, out bool value)
    {
        value = false;
        if (!element.TryGetProperty(name, out var property))
        {
            return true;
        }

        if (property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }

        value = property.GetBoolean();
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

        if (property.ValueKind != JsonValueKind.Array ||
            property.GetArrayLength() > MaximumArrayItems)
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
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (name == "collectionDelimiters")
            {
                if (value.Length > MaximumDelimiterLength ||
                    value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length == 0 ||
                    value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(token =>
                        !IsValidIdentifier(token) && !token.All(char.IsPunctuation)))
                {
                    return false;
                }
            }
            else if (!IsValidIdentifier(value) &&
                !(name == "matchTokens" && value is "+" or "-" or "*" or "/" or "==" or "!=" or "<" or ">" or "<=" or ">="))
            {
                return false;
            }

            builder.Add(value);
        }

        values = builder.ToImmutable();
        return true;
    }

    private static bool IsValidSemanticExpressionKind(string? kind) =>
        kind is null or "arithmetic" or "boolean" or "value" or "functionCall" or "comparisonOperator";

    private static bool IsValidIdentifier(string value) =>
        value.Length is > 0 and <= MaximumIdentifierLength &&
        !string.IsNullOrWhiteSpace(value) &&
        value.All(character =>
            char.IsAsciiLetterOrDigit(character) ||
            character is '.' or '-' or '_' or ':');

    private static bool IsValidDelimiter(string value) =>
        value.Length is > 0 and <= MaximumDelimiterLength;
}
