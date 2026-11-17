using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lattice.Core.Reasoning;

public sealed class SemanticWorldModelLoader
{
    private const int SupportedFormatVersion = 4;
    private const int MaximumConcepts = 2000;
    private const int MaximumRelations = 500;
    private const int MaximumDefinitions = 500;
    private const int MaximumDataProperties = 250;
    private const int MaximumAssertions = 5000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public bool TryLoad(
        string? json,
        out SemanticWorldModel? model,
        out string? error)
    {
        model = null;
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "The semantic world model is empty.";
            return false;
        }

        SemanticWorldModelDocument? document;

        try
        {
            document = JsonSerializer.Deserialize<SemanticWorldModelDocument>(json, JsonOptions);
        }
        catch (JsonException)
        {
            error = "The semantic world model is not valid JSON or contains an unsupported field.";
            return false;
        }

        if (document is null ||
            document.FormatVersion is not (1 or 2 or 3 or SupportedFormatVersion) ||
            !SemanticWorldModelValidation.IsValidId(document.Id) ||
            string.IsNullOrWhiteSpace(document.Name) ||
            document.Name.Length > 120 ||
            document.Concepts is null ||
            document.Lexemes is null ||
            document.Relations is null ||
            document.Definitions is null ||
            document.RelationAssertions is null ||
            document.DataProperties is null ||
            document.DataAssertions is null ||
            document.Concepts.Count is 0 or > MaximumConcepts ||
            document.Relations.Count > MaximumRelations ||
            document.Definitions.Count > MaximumDefinitions ||
            document.DataProperties.Count > MaximumDataProperties ||
            (document.TypeAssertions?.Count ?? 0) +
                document.RelationAssertions.Count +
                document.DataAssertions.Count > MaximumAssertions ||
            (document.FormatVersion >= 3 && document.TypeAssertions is not null) ||
            (document.FormatVersion == 3 &&
                document.DataProperties.Any(property => property is not null &&
                    !string.Equals(property.ValueType, "number", StringComparison.OrdinalIgnoreCase))))
        {
            error = "The semantic world model metadata, collections, or version-specific fields are invalid.";
            return false;
        }

        if (!TryCreateConcepts(document, out var concepts, out error))
        {
            return false;
        }

        var conceptMap = concepts.ToDictionary(concept => concept.Id, StringComparer.Ordinal);
        if (!TryCreateRelations(document, conceptMap, out var relations, out error))
        {
            return false;
        }

        var relationMap = relations.ToDictionary(relation => relation.Id, StringComparer.Ordinal);
        if (!TryCreateDefinitions(document, conceptMap, relationMap, out var definitions, out error))
        {
            return false;
        }

        if (!TryCreateAssertions(
            document,
            conceptMap,
            relationMap,
            out var relationAssertions,
            out error))
        {
            return false;
        }

        if (SemanticWorldModelValidation.HasInheritanceCycle(relationAssertions))
        {
            error = "The semantic world model contains a cyclic type hierarchy.";
            return false;
        }

        if (!SemanticWorldModelDataLoader.TryCreateDataProperties(
            document,
            conceptMap,
            out var dataProperties,
            out error) ||
            !SemanticWorldModelDataLoader.TryCreateDataAssertions(
                document,
                conceptMap,
                dataProperties,
                out var dataAssertions,
                out error))
        {
            return false;
        }

        if (!TryCreateLexemes(document, conceptMap, out var lexemes, out error))
        {
            return false;
        }

        var candidate = new SemanticWorldModel(
            document.FormatVersion,
            document.Id,
            document.Name,
            concepts,
            lexemes,
            relations,
            definitions,
            relationAssertions,
            dataProperties,
            dataAssertions);

        if (!SemanticWorldModelValidation.AreAssertionsWellTyped(candidate))
        {
            error = "A semantic assertion violates the declared relation domain or range.";
            return false;
        }

        model = candidate;
        return true;
    }

    private static bool TryCreateConcepts(
        SemanticWorldModelDocument document,
        out ImmutableArray<SemanticConcept> concepts,
        out string? error)
    {
        concepts = ImmutableArray<SemanticConcept>.Empty;
        error = null;
        var builder = ImmutableArray.CreateBuilder<SemanticConcept>(document.Concepts.Count);
        var identifiers = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in document.Concepts)
        {
            if (item is null ||
                !SemanticWorldModelValidation.IsValidId(item.Id) ||
                string.IsNullOrWhiteSpace(item.Label) ||
                item.Label.Length > 100 ||
                !Enum.TryParse<SemanticConceptKind>(item.Kind, true, out var kind) ||
                !identifiers.Add(item.Id))
            {
                error = "Every concept requires a unique id, a label, and a valid type or individual kind.";
                return false;
            }

            builder.Add(new SemanticConcept(item.Id, item.Label, kind));
        }

        concepts = builder.ToImmutable();
        return true;
    }

    private static bool TryCreateRelations(
        SemanticWorldModelDocument document,
        IReadOnlyDictionary<string, SemanticConcept> concepts,
        out ImmutableArray<SemanticRelationDefinition> relations,
        out string? error)
    {
        relations = ImmutableArray<SemanticRelationDefinition>.Empty;
        error = null;
        var builder = ImmutableArray.CreateBuilder<SemanticRelationDefinition>(document.Relations.Count + 2);
        var identifiers = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in document.Relations)
        {
            if (item is null ||
                !SemanticWorldModelValidation.IsValidId(item.Id) ||
                !Enum.TryParse<SemanticRelationArgumentKind>(item.SubjectKind, true, out var subjectKind) ||
                !Enum.IsDefined(subjectKind) ||
                !Enum.TryParse<SemanticRelationArgumentKind>(item.ObjectKind, true, out var objectKind) ||
                !Enum.IsDefined(objectKind) ||
                string.IsNullOrWhiteSpace(item.Verb) ||
                item.Verb.Length > 80 ||
                (item.ObjectRole is not null && item.ObjectRole.Length > 80) ||
                item.Order is < 0 or > 1000 ||
                item.ComparisonPriority is < 0 or > 1000 ||
                (item.Domain is not null && !SemanticWorldModelValidation.IsType(concepts, item.Domain)) ||
                (item.Range is not null && !SemanticWorldModelValidation.IsType(concepts, item.Range)) ||
                (subjectKind == SemanticRelationArgumentKind.Type && item.Domain is not null) ||
                (objectKind == SemanticRelationArgumentKind.Type && item.Range is not null) ||
                !identifiers.Add(item.Id))
            {
                error = "Every relation requires a unique id, valid argument kinds, optional valid domain/range types, and valid language metadata.";
                return false;
            }

            var validDomain = item.Id == "instance-of"
                ? subjectKind == SemanticRelationArgumentKind.Individual && item.Domain is null
                : subjectKind switch
                {
                    SemanticRelationArgumentKind.Individual => SemanticWorldModelValidation.IsType(concepts, item.Domain),
                    SemanticRelationArgumentKind.Type => item.Domain is null,
                    _ => false
                };
            var validRange = objectKind switch
            {
                SemanticRelationArgumentKind.Individual => SemanticWorldModelValidation.IsType(concepts, item.Range),
                SemanticRelationArgumentKind.Type => item.Range is null,
                _ => false
            };

            if (!validDomain || !validRange ||
                (item.Id == "is-a" &&
                    (subjectKind != SemanticRelationArgumentKind.Type ||
                     objectKind != SemanticRelationArgumentKind.Type)) ||
                (item.Id == "instance-of" &&
                    (subjectKind != SemanticRelationArgumentKind.Individual ||
                     objectKind != SemanticRelationArgumentKind.Type ||
                     item.Domain is not null ||
                     item.Range is not null)))
            {
                error = $"Relation '{item.Id}' has an invalid argument-kind declaration.";
                return false;
            }

            builder.Add(new SemanticRelationDefinition(
                item.Id,
                item.Domain,
                item.Range,
                item.Verb,
                item.ObjectRole,
                item.Order,
                item.ComparisonPriority,
                subjectKind,
                objectKind));
        }

        if (document.FormatVersion < 3)
        {
            if (!identifiers.Contains("is-a"))
            {
                builder.Add(new SemanticRelationDefinition(
                    "is-a", null, null, "is a", null, 0, 0,
                    SemanticRelationArgumentKind.Type,
                    SemanticRelationArgumentKind.Type));
            }

            if (!identifiers.Contains("instance-of"))
            {
                builder.Add(new SemanticRelationDefinition(
                    "instance-of", null, null, "is an instance of", null, 0, 0,
                    SemanticRelationArgumentKind.Individual,
                    SemanticRelationArgumentKind.Type));
            }
        }

        if (document.FormatVersion >= 3 &&
            (!builder.Any(relation =>
                relation.Id == "is-a" &&
                relation.SubjectKind == SemanticRelationArgumentKind.Type &&
                relation.ObjectKind == SemanticRelationArgumentKind.Type) ||
             !builder.Any(relation =>
                relation.Id == "instance-of" &&
                relation.SubjectKind == SemanticRelationArgumentKind.Individual &&
                relation.ObjectKind == SemanticRelationArgumentKind.Type)))
        {
            error = "Version 3+ semantic world models require both is-a (type-to-type) and instance-of (individual-to-type) relations.";
            return false;
        }

        relations = builder.ToImmutable();
        return true;
    }

    private static bool TryCreateDefinitions(
        SemanticWorldModelDocument document,
        IReadOnlyDictionary<string, SemanticConcept> concepts,
        IReadOnlyDictionary<string, SemanticRelationDefinition> relations,
        out ImmutableArray<SemanticTypeDefinition> definitions,
        out string? error)
    {
        definitions = ImmutableArray<SemanticTypeDefinition>.Empty;
        error = null;
        var builder = ImmutableArray.CreateBuilder<SemanticTypeDefinition>(document.Definitions.Count);
        var identifiers = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in document.Definitions)
        {
            var parents = item?.Parents ?? [];
            var hasValidLegacyParents = document.FormatVersion < 3 &&
                parents.All(parent => SemanticWorldModelValidation.IsType(concepts, parent)) &&
                parents.Distinct(StringComparer.Ordinal).Count() == parents.Count;

            if (item is null ||
                !SemanticWorldModelValidation.IsType(concepts, item.Concept) ||
                item.Conditions is null ||
                item.NecessaryConditions is null ||
                (document.FormatVersion < 3 && !hasValidLegacyParents) ||
                (document.FormatVersion >= 3 &&
                    (item.Parents is not null || item.SourceText is not null || item.SourceUrl is not null)) ||
                !identifiers.Add(item.Concept))
            {
                error = document.FormatVersion >= 3
                    ? "Version 3+ type definitions must contain semantic structure only; omit parents and provenance fields."
                    : "Every type definition must reference one unique type concept and valid legacy parent types.";
                return false;
            }

            var necessaryConditions = ImmutableArray.CreateBuilder<SemanticRestriction>();
            if (document.FormatVersion >= 2 &&
                !TryCreateRestrictions(item.Concept, item.NecessaryConditions, concepts, relations, necessaryConditions, out error))
            {
                return false;
            }

            SemanticDefinitionKind definitionKind;
            SemanticClassExpression? sufficientExpression;
            ImmutableArray<SemanticRestriction> sufficientRestrictions;

            if (document.FormatVersion == 1)
            {
                var legacyConditions = ImmutableArray.CreateBuilder<SemanticRestriction>();
                if (!TryCreateRestrictions(item.Concept, item.Conditions, concepts, relations, legacyConditions, out error))
                {
                    return false;
                }

                definitionKind = legacyConditions.Count == 0
                    ? SemanticDefinitionKind.Primitive
                    : SemanticDefinitionKind.Defined;
                sufficientRestrictions = legacyConditions.ToImmutable();
                sufficientExpression = CreateConjunction(sufficientRestrictions);
            }
            else
            {
                if (item.Conditions.Count != 0 ||
                    !Enum.TryParse(item.DefinitionKind, true, out definitionKind) ||
                    !Enum.IsDefined(definitionKind))
                {
                    error = $"Type definition '{item.Concept}' requires an explicit primitive or defined status and must use sufficientExpression rather than legacy conditions.";
                    return false;
                }

                if (!TryCreateExpression(item.Concept, item.SufficientExpression, concepts, relations,
                    out sufficientExpression, out sufficientRestrictions, out error))
                {
                    return false;
                }
            }

            if ((definitionKind == SemanticDefinitionKind.Defined && sufficientExpression is null) ||
                (definitionKind == SemanticDefinitionKind.Primitive && sufficientExpression is not null))
            {
                error = $"Type definition '{item.Concept}' has an inconsistent primitive/defined declaration.";
                return false;
            }

            builder.Add(new SemanticTypeDefinition(
                item.Concept,
                GetSimpleSufficientRestrictions(sufficientExpression),
                new SemanticWorldModelSource($"{document.Id}:definition:{item.Concept}"))
            {
                DefinitionKind = definitionKind,
                NecessaryConditions = necessaryConditions.ToImmutable(),
                SufficientExpression = sufficientExpression
            });
        }

        definitions = builder.ToImmutable();
        if (document.FormatVersion >= 2)
        {
            var definedTypes = definitions.Select(definition => definition.Concept)
                .ToHashSet(StringComparer.Ordinal);
            if (concepts.Values.Any(concept =>
                concept.Kind == SemanticConceptKind.Type &&
                !definedTypes.Contains(concept.Id)))
            {
                error = "Every version 2 or later type concept requires an explicit primitive or defined declaration.";
                return false;
            }
        }

        return true;
    }

    private static bool TryCreateRestrictions(
        string ownerType,
        IReadOnlyCollection<RestrictionDocument> documents,
        IReadOnlyDictionary<string, SemanticConcept> concepts,
        IReadOnlyDictionary<string, SemanticRelationDefinition> relations,
        ImmutableArray<SemanticRestriction>.Builder restrictions,
        out string? error)
    {
        error = null;
        foreach (var condition in documents)
        {
            if (condition is null ||
                !relations.TryGetValue(condition.Relation, out var relation) ||
                relation.SubjectKind != SemanticRelationArgumentKind.Individual ||
                relation.ObjectKind != SemanticRelationArgumentKind.Individual ||
                relation.DomainType is null ||
                relation.RangeType is null ||
                !SemanticWorldModelValidation.IsType(concepts, condition.TargetType))
            {
                error = $"Type definition '{ownerType}' contains an invalid necessary or sufficient relation restriction.";
                return false;
            }

            restrictions.Add(new SemanticRestriction(condition.Relation, condition.TargetType));
        }

        return true;
    }

    private static bool TryCreateExpression(
        string ownerType,
        SemanticClassExpression? expression,
        IReadOnlyDictionary<string, SemanticConcept> concepts,
        IReadOnlyDictionary<string, SemanticRelationDefinition> relations,
        out SemanticClassExpression? parsed,
        out ImmutableArray<SemanticRestriction> restrictions,
        out string? error)
    {
        parsed = null;
        restrictions = [];
        error = null;
        if (expression is null)
        {
            return true;
        }

        var collected = ImmutableArray.CreateBuilder<SemanticRestriction>();
        var nodeCount = 0;
        if (!ValidateExpression(expression, 0, ref nodeCount, relations, concepts, collected))
        {
            error = $"Type definition '{ownerType}' contains an invalid class expression. Use valid some, all, cycle, and/or expressions.";
            return false;
        }

        parsed = expression;
        restrictions = collected.ToImmutable();
        return true;
    }

    private static bool ValidateExpression(
        SemanticClassExpression expression,
        int depth,
        ref int nodeCount,
        IReadOnlyDictionary<string, SemanticRelationDefinition> relations,
        IReadOnlyDictionary<string, SemanticConcept> concepts,
        ImmutableArray<SemanticRestriction>.Builder restrictions)
    {
        if (depth > 8 || ++nodeCount > 64)
        {
            return false;
        }

        if (expression.Kind is SemanticClassExpressionKind.Some or SemanticClassExpressionKind.All)
        {
            if (string.IsNullOrWhiteSpace(expression.Relation) ||
                !relations.TryGetValue(expression.Relation, out var relation) ||
                relation.SubjectKind != SemanticRelationArgumentKind.Individual ||
                relation.ObjectKind != SemanticRelationArgumentKind.Individual ||
                relation.DomainType is null ||
                relation.RangeType is null ||
                string.IsNullOrWhiteSpace(expression.TargetType) ||
                !SemanticWorldModelValidation.IsType(concepts, expression.TargetType) ||
                !expression.Operands.IsDefaultOrEmpty ||
                expression.StartRelation is not null ||
                expression.MinimumLength != 0 ||
                expression.SourceConnectionRelation is not null ||
                expression.TargetConnectionRelation is not null ||
                (expression.Filler is not null &&
                 !ValidateExpression(expression.Filler, depth + 1, ref nodeCount, relations, concepts, restrictions)))
            {
                return false;
            }

            restrictions.Add(new SemanticRestriction(expression.Relation, expression.TargetType));
            return true;
        }

        if (expression.Kind == SemanticClassExpressionKind.Cycle)
        {
            return !string.IsNullOrWhiteSpace(expression.Relation) &&
                relations.TryGetValue(expression.Relation, out var traversalRelation) &&
                traversalRelation.SubjectKind == SemanticRelationArgumentKind.Individual &&
                traversalRelation.ObjectKind == SemanticRelationArgumentKind.Individual &&
                traversalRelation.DomainType is not null &&
                traversalRelation.RangeType is not null &&
                !string.IsNullOrWhiteSpace(expression.StartRelation) &&
                relations.TryGetValue(expression.StartRelation, out var startRelation) &&
                startRelation.SubjectKind == SemanticRelationArgumentKind.Individual &&
                startRelation.ObjectKind == SemanticRelationArgumentKind.Individual &&
                startRelation.DomainType is not null &&
                startRelation.RangeType is not null &&
                !string.IsNullOrWhiteSpace(expression.TargetType) &&
                SemanticWorldModelValidation.IsType(concepts, expression.TargetType) &&
                expression.MinimumLength is >= 2 and <= 64 &&
                ((expression.SourceConnectionRelation is null &&
                  expression.TargetConnectionRelation is null) ||
                 (SemanticWorldModelValidation.IsValidConnectionRelation(expression.SourceConnectionRelation, relations) &&
                  SemanticWorldModelValidation.IsValidConnectionRelation(expression.TargetConnectionRelation, relations))) &&
                expression.Filler is null &&
                expression.Operands.IsDefaultOrEmpty;
        }

        if (expression.Kind is not (SemanticClassExpressionKind.And or SemanticClassExpressionKind.Or) ||
            expression.Relation is not null ||
            expression.StartRelation is not null ||
            expression.TargetType is not null ||
            expression.MinimumLength != 0 ||
            expression.SourceConnectionRelation is not null ||
            expression.TargetConnectionRelation is not null ||
            expression.Filler is not null ||
            expression.Operands.IsDefault ||
            expression.Operands.Length < 2)
        {
            return false;
        }

        foreach (var operand in expression.Operands)
        {
            if (operand is null ||
                !ValidateExpression(operand, depth + 1, ref nodeCount, relations, concepts, restrictions))
            {
                return false;
            }
        }

        return true;
    }

    private static SemanticClassExpression? CreateConjunction(
        ImmutableArray<SemanticRestriction> restrictions)
    {
        if (restrictions.IsDefaultOrEmpty)
        {
            return null;
        }

        var operands = restrictions.Select(restriction => new SemanticClassExpression(
            SemanticClassExpressionKind.Some,
            restriction.Relation,
            restriction.TargetType)).ToImmutableArray();

        return operands.Length == 1
            ? operands[0]
            : new SemanticClassExpression(SemanticClassExpressionKind.And, Operands: operands);
    }

    private static ImmutableArray<SemanticRestriction> GetSimpleSufficientRestrictions(
        SemanticClassExpression? expression)
    {
        if (expression is null)
        {
            return [];
        }

        var operands = expression.Kind == SemanticClassExpressionKind.And
            ? expression.Operands
            : ImmutableArray.Create(expression);
        if (operands.Any(operand => operand.Kind != SemanticClassExpressionKind.Some))
        {
            return [];
        }

        return operands.Select(operand => new SemanticRestriction(
            operand.Relation!,
            operand.TargetType!)).ToImmutableArray();
    }

    private static bool TryCreateAssertions(
        SemanticWorldModelDocument document,
        IReadOnlyDictionary<string, SemanticConcept> concepts,
        IReadOnlyDictionary<string, SemanticRelationDefinition> relations,
        out ImmutableArray<SemanticRelationAssertion> relationAssertions,
        out string? error)
    {
        relationAssertions = ImmutableArray<SemanticRelationAssertion>.Empty;
        error = null;
        var legacyTypeAssertions = document.TypeAssertions ?? [];
        var relationBuilder = ImmutableArray.CreateBuilder<SemanticRelationAssertion>(
            document.RelationAssertions.Count +
            legacyTypeAssertions.Count +
            (document.FormatVersion < 3
                ? document.Definitions.Sum(definition => definition.Parents?.Count ?? 0)
                : 0));
        var relationKeys = new HashSet<string>(StringComparer.Ordinal);

        if (document.FormatVersion >= 3 && document.TypeAssertions is not null)
        {
            error = "Version 3+ models represent membership with instance-of relation assertions, not typeAssertions.";
            return false;
        }

        foreach (var item in document.RelationAssertions)
        {
            if (item is null ||
                !relations.TryGetValue(item.Relation, out var relation) ||
                !SemanticWorldModelValidation.IsRelationArgument(concepts, item.Subject, relation.SubjectKind) ||
                !SemanticWorldModelValidation.IsRelationArgument(concepts, item.Object, relation.ObjectKind) ||
                (document.FormatVersion >= 3 &&
                    (item.SourceText is not null || item.SourceUrl is not null)) ||
                !relationKeys.Add($"{item.Subject}:{item.Relation}:{item.Object}"))
            {
                error = "Every relation assertion requires arguments of the declared kinds and a unique declared relation.";
                return false;
            }

            var sourceId = $"{document.Id}:relation:{item.Subject}:{item.Relation}:{item.Object}";
            relationBuilder.Add(new SemanticRelationAssertion(
                item.Subject,
                item.Relation,
                item.Object,
                new SemanticWorldModelSource(sourceId)));
        }

        foreach (var item in legacyTypeAssertions)
        {
            if (document.FormatVersion >= 3 ||
                item is null ||
                !SemanticWorldModelValidation.IsIndividual(concepts, item.Individual) ||
                !SemanticWorldModelValidation.IsType(concepts, item.Type))
            {
                error = "Legacy type assertions must reference a declared individual and type.";
                return false;
            }

            var key = $"{item.Individual}:instance-of:{item.Type}";
            if (!relationKeys.Add(key))
            {
                error = "A legacy instance-of assertion is duplicated.";
                return false;
            }

            relationBuilder.Add(new SemanticRelationAssertion(
                item.Individual,
                "instance-of",
                item.Type,
                new SemanticWorldModelSource($"{document.Id}:instance-of:{item.Individual}:{item.Type}")));
        }

        if (document.FormatVersion < 3)
        {
            foreach (var definition in document.Definitions)
            {
                foreach (var parent in definition.Parents ?? [])
                {
                    var key = $"{definition.Concept}:is-a:{parent}";
                    if (!relationKeys.Add(key))
                    {
                        error = "A legacy type hierarchy contains duplicate parent relations.";
                        return false;
                    }

                    relationBuilder.Add(new SemanticRelationAssertion(
                        definition.Concept,
                        "is-a",
                        parent,
                        new SemanticWorldModelSource($"{document.Id}:is-a:{definition.Concept}:{parent}")));
                }
            }
        }

        relationAssertions = relationBuilder.ToImmutable();
        return true;
    }

    private static bool TryCreateLexemes(
        SemanticWorldModelDocument document,
        IReadOnlyDictionary<string, SemanticConcept> concepts,
        out ImmutableArray<SemanticLexeme> lexemes,
        out string? error)
    {
        lexemes = ImmutableArray<SemanticLexeme>.Empty;
        error = null;
        var builder = ImmutableArray.CreateBuilder<SemanticLexeme>(document.Lexemes.Count);
        var forms = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in document.Lexemes)
        {
            if (item is null ||
                string.IsNullOrWhiteSpace(item.Form) ||
                item.Form.Length > 100 ||
                !concepts.ContainsKey(item.Concept) ||
                string.IsNullOrWhiteSpace(item.PartOfSpeech) ||
                item.PartOfSpeech.Length > 30 ||
                !forms.Add(SemanticWorldModel.Normalize(item.Form)))
            {
                error = "Every lexeme requires a unique word form, a declared concept, and a part of speech.";
                return false;
            }

            builder.Add(new SemanticLexeme(item.Form, item.Concept, item.PartOfSpeech));
        }

        lexemes = builder.ToImmutable();
        return true;
    }







    internal sealed class SemanticWorldModelDocument
    {
        public SemanticWorldModelDocument() { }
        public int FormatVersion { get; set; }
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<ConceptDocument> Concepts { get; set; } = [];
        public List<LexemeDocument> Lexemes { get; set; } = [];
        public List<RelationDocument> Relations { get; set; } = [];
        public List<TypeDefinitionDocument> Definitions { get; set; } = [];
        public List<TypeAssertionDocument>? TypeAssertions { get; set; }
        public List<RelationAssertionDocument> RelationAssertions { get; set; } = [];
        public List<DataPropertyDocument> DataProperties { get; set; } = [];
        public List<DataAssertionDocument> DataAssertions { get; set; } = [];
    }

    private sealed class ConceptDocument
    {
        public ConceptDocument() { }
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
    }

    private sealed class LexemeDocument
    {
        public LexemeDocument() { }
        public string Form { get; set; } = string.Empty;
        public string Concept { get; set; } = string.Empty;
        public string PartOfSpeech { get; set; } = string.Empty;
    }

    private sealed class RelationDocument
    {
        public RelationDocument() { }
        public string Id { get; set; } = string.Empty;
        public string? Domain { get; set; }
        public string? Range { get; set; }
        public string Verb { get; set; } = string.Empty;
        public string? ObjectRole { get; set; }
        public int Order { get; set; }
        public int ComparisonPriority { get; set; }
        public string SubjectKind { get; set; } = "individual";
        public string ObjectKind { get; set; } = "individual";
    }

    private sealed class TypeDefinitionDocument
    {
        public TypeDefinitionDocument() { }
        public string Concept { get; set; } = string.Empty;
        public List<string>? Parents { get; set; }
        public string? DefinitionKind { get; set; }
        public List<RestrictionDocument> Conditions { get; set; } = [];
        public List<RestrictionDocument> NecessaryConditions { get; set; } = [];
        public SemanticClassExpression? SufficientExpression { get; set; }
        public string? SourceText { get; set; }
        public string? SourceUrl { get; set; }
    }

    private sealed class RestrictionDocument
    {
        public RestrictionDocument() { }
        public string Relation { get; set; } = string.Empty;
        public string TargetType { get; set; } = string.Empty;
    }

    internal sealed class DataPropertyDocument
    {
        public DataPropertyDocument() { }
        public string Id { get; set; } = string.Empty;
        public string DomainType { get; set; } = string.Empty;
        public string ValueType { get; set; } = string.Empty;
        public string? DimensionUnit { get; set; }
    }

    internal sealed class DataAssertionDocument
    {
        public DataAssertionDocument() { }
        public string Subject { get; set; } = string.Empty;
        public string Property { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string? Unit { get; set; }
        public string? SourceText { get; set; }
        public string? SourceUrl { get; set; }
    }

    private sealed class TypeAssertionDocument
    {
        public TypeAssertionDocument() { }
        public string Individual { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? SourceText { get; set; }
        public string? SourceUrl { get; set; }
    }

    private sealed class RelationAssertionDocument
    {
        public RelationAssertionDocument() { }
        public string Subject { get; set; } = string.Empty;
        public string Relation { get; set; } = string.Empty;
        public string Object { get; set; } = string.Empty;
        public string? SourceText { get; set; }
        public string? SourceUrl { get; set; }
    }
}
