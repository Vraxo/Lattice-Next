using System.Collections.Immutable;
using System.Text.Json;

namespace Lattice.Core.Generation;

public static class SequenceKnowledgeBaseLoader
{
    private const int SupportedFormatVersion = 1;
    private const int MaximumJsonLength = 2_097_152;
    private const int MaximumRules = 10_000;
    private const int MaximumInputRules = 4096;
    private const int MaximumValidations = 4096;
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
                !HasOnlyProperties(root, "formatVersion", "rules", "encodings", "inputRules", "validations") ||
                !TryGetInt32(root, "formatVersion", out var formatVersion) ||
                formatVersion != SupportedFormatVersion ||
                !TryGetArray(root, "rules", MaximumRules, required: true, out var rulesElement) ||
                rulesElement.GetArrayLength() == 0 ||
                !TryGetArray(root, "encodings", MaximumEncodings, required: false, out var encodingElements) ||
                !TryReadEncodings(encodingElements, out var encodings, out error) ||
                !TryGetArray(root, "inputRules", MaximumInputRules, required: false, out var inputRuleElements) ||
                !TryReadInputRules(inputRuleElements, out var inputRules, out error) ||
                !TryGetArray(root, "validations", MaximumValidations, required: false, out var validationElements) ||
                !TryReadInputValidations(validationElements, out var validations, out error))
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
                        "forbiddenFacts",
                        "advanceCollections",
                        "weight") ||
                    !TryGetString(element, "id", out var id) ||
                    !IsValidIdentifier(id) ||
                    !identifiers.Add(id) ||
                    !TryGetOptionalToken(element, "nextToken", out var nextToken) ||
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
                    !TryGetStringArray(element, "forbiddenFacts", out var forbiddenFacts) ||
                    !TryGetStringArray(element, "advanceCollections", out var advanceCollections) ||
                    !TryGetWeight(element, out var weight) ||
                    !HasUniqueIdentifiers(requiredFacts) ||
                    !HasUniqueIdentifiers(requiresCompletedFacts) ||
                    !HasUniqueIdentifiers(satisfiesFacts) ||
                    !HasUniqueIdentifiers(forbiddenFacts) ||
                    advanceCollections.Any(value => !IsValidCollectionKey(value)) ||
                    advanceCollections.Distinct(StringComparer.Ordinal).Count() != advanceCollections.Length ||
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
                    TokenBinding = tokenBinding,
                    ForbiddenFacts = forbiddenFacts,
                    AdvanceCollections = advanceCollections
                });
            }

            knowledge = new SequenceKnowledgeBase(rules.ToImmutable(), encodings, inputRules, validations);
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
        var inputRules = ImmutableArray.CreateBuilder<SequenceInputFactRule>();
        var validations = ImmutableArray.CreateBuilder<SequenceInputFactValidation>();
        var ruleIdentifiers = new HashSet<string>(StringComparer.Ordinal);
        var encodingIdentifiers = new HashSet<string>(StringComparer.Ordinal);
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

        foreach (var rule in rules)
        {
            if (rule.TokenBinding?.Encoding is { } encoding &&
                !encodingIdentifiers.Contains(encoding))
            {
                error = $"The token binding in rule '{rule.Id}' references missing encoding '{encoding}'.";
                return false;
            }
        }

        knowledge = new SequenceKnowledgeBase(
            rules.ToImmutable(),
            encodings.ToImmutable(),
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
                !HasOnlyProperties(item, "id", "matchTokens", "factId", "valueId", "literalValue", "captures", "searchAllSentences", "requiredAnySequences", "requiredAllSequences", "forbiddenAnySequences") ||
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
                ForbiddenAnySequences = forbiddenAnySequences
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
                !HasOnlyProperties(item, "factId", "valueId", "startAfterTokens", "startAfterSequences", "stopBeforeTokens", "stopBeforeSequences", "literalValue", "collectionDelimiter", "collectionFieldDelimiter", "collectionFieldIndex", "collectionTemplate", "trimPrefixTokens", "trimSuffixCharacters", "collectionDelimiters", "preserveRawValue", "normalization", "numericOffset") ||
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
                !TryGetNestedStringArray(item, "stopBeforeSequences", out var stopBeforeSequences) ||
                !TryGetStringArray(item, "trimPrefixTokens", out var trimPrefixTokens) ||
                !TryGetOptionalString(item, "trimSuffixCharacters", out var trimSuffixCharacters) ||
                !TryGetOptionalBoolean(item, "preserveRawValue", out var preserveRawValue) ||
                !TryGetOptionalString(item, "normalization", out var normalization) ||
                (normalization is not null && normalization is not ("booleanExpression" or "comparisonOperator")) ||
                !TryGetOptionalInt32(item, "numericOffset", out var numericOffset) ||
                ((collectionFieldIndex is not null || collectionTemplate is not null) &&
                    ((collectionDelimiter is null && collectionDelimiters.IsDefaultOrEmpty) ||
                     collectionFieldDelimiter is null)) ||
                (collectionFieldDelimiter is not null &&
                    collectionFieldIndex is null && collectionTemplate is null) ||
                (collectionTemplate is not null && collectionFieldIndex is not null))
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
                TrimPrefixTokens = trimPrefixTokens,
                TrimSuffixCharacters = trimSuffixCharacters ?? string.Empty,
                PreserveRawValue = preserveRawValue,
                Normalization = normalization,
                NumericOffset = numericOffset ?? 0
            });
        }

        captures = builder.ToImmutable();
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
            "comparisonOperandsReferences",
            "arithmeticExpressionReferences",
            "booleanExpressionReferences",
            "valueExpressionReferences",
            "matchingFunctionCall",
            "matchingNumericFunctionCall",
            "pythonListLiteral",
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
            !HasOnlyProperties(property, "factId", "valueId", "encoding", "collection", "collectionSeparator", "prefix", "suffix", "secondaryValueId", "secondaryCollectionSeparator", "secondaryValueIsCollection", "valueTemplate", "minimumCollectionCursor") ||
            !TryGetString(property, "factId", out var factId) ||
            !IsValidIdentifier(factId) ||
            !TryGetString(property, "valueId", out var valueId) ||
            !IsValidIdentifier(valueId) ||
            !TryGetOptionalString(property, "encoding", out var encoding) ||
            (encoding is not null && !IsValidIdentifier(encoding)) ||
            !TryGetOptionalBoolean(property, "collection", out var useCollection) ||
            !TryGetOptionalToken(property, "collectionSeparator", out var collectionSeparator) ||
            !TryGetOptionalToken(property, "prefix", out var prefix) ||
            !TryGetOptionalToken(property, "suffix", out var suffix) ||
            !TryGetOptionalString(property, "secondaryValueId", out var secondaryValueId) ||
            (secondaryValueId is not null && !IsValidIdentifier(secondaryValueId)) ||
            !TryGetOptionalToken(property, "secondaryCollectionSeparator", out var secondaryCollectionSeparator) ||
            !TryGetOptionalBoolean(property, "secondaryValueIsCollection", out var secondaryValueIsCollectionValue) ||
            !TryGetOptionalString(property, "valueTemplate", out var valueTemplate) ||
            !TryGetOptionalInt32(property, "minimumCollectionCursor", out var minimumCollectionCursor) ||
            (collectionSeparator is not null &&
                (collectionSeparator.Length > MaximumDelimiterLength || useCollection || encoding is not null)) ||
            (prefix is not null && prefix.Length > 128) ||
            (suffix is not null && suffix.Length > 128) ||
            (minimumCollectionCursor is > 0 && !useCollection) ||
            (valueTemplate is null && (secondaryValueId is not null || secondaryCollectionSeparator is not null || property.TryGetProperty("secondaryValueIsCollection", out _))) ||
            (valueTemplate is not null &&
                (secondaryValueId is null ||
                 ((!property.TryGetProperty("secondaryValueIsCollection", out _) || secondaryValueIsCollectionValue) && secondaryCollectionSeparator is null) ||
                 (property.TryGetProperty("secondaryValueIsCollection", out _) && !secondaryValueIsCollectionValue && secondaryCollectionSeparator is not null) ||
                 (secondaryCollectionSeparator?.Length > MaximumDelimiterLength) ||
                 useCollection || encoding is not null || collectionSeparator is not null ||
                 valueTemplate.Length > 256 ||
                 !valueTemplate.Contains("{0}", StringComparison.Ordinal) ||
                 !valueTemplate.Contains("{1}", StringComparison.Ordinal))))
        {
            return false;
        }

        binding = new SequenceTokenBinding(factId, valueId, encoding, useCollection)
        {
            CollectionSeparator = collectionSeparator,
            Prefix = prefix ?? string.Empty,
            Suffix = suffix ?? string.Empty,
            SecondaryValueId = secondaryValueId,
            SecondaryCollectionSeparator = secondaryCollectionSeparator,
            SecondaryValueIsCollection = !property.TryGetProperty("secondaryValueIsCollection", out _) || secondaryValueIsCollectionValue,
            ValueTemplate = valueTemplate,
            MinimumCollectionCursor = minimumCollectionCursor ?? 0
        };
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

    private static bool TryGetOptionalToken(
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
        return value is not null &&
            value.Length is > 0 and <= MaximumTokenLength;
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

            if (name == "collectionDelimiters")
            {
                if (value.Length > MaximumDelimiterLength ||
                    value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length == 0 ||
                    value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(token => !IsValidIdentifier(token)))
                {
                    return false;
                }
            }
            else if (name != "contextSuffix" &&
                !IsValidIdentifier(value) &&
                !(name == "matchTokens" && value is "+" or "-" or "*" or "/" or "==" or "!=" or "<" or ">" or "<=" or ">="))
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

    private static bool IsValidCollectionKey(string value)
    {
        var separator = value.IndexOf(':');
        return separator > 0 && separator < value.Length - 1 &&
            IsValidIdentifier(value[..separator]) && IsValidIdentifier(value[(separator + 1)..]);
    }

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
