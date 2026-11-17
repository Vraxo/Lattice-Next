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

        if (!SemanticWorldModelStructureLoader.TryCreateConcepts(document, out var concepts, out error))
        {
            return false;
        }

        var conceptMap = concepts.ToDictionary(concept => concept.Id, StringComparer.Ordinal);
        if (!SemanticWorldModelStructureLoader.TryCreateRelations(document, conceptMap, out var relations, out error))
        {
            return false;
        }

        var relationMap = relations.ToDictionary(relation => relation.Id, StringComparer.Ordinal);
        if (!SemanticWorldModelDefinitionLoader.TryCreateDefinitions(document, conceptMap, relationMap, out var definitions, out error))
        {
            return false;
        }

        if (!SemanticWorldModelStructureLoader.TryCreateAssertions(
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

        if (!SemanticWorldModelStructureLoader.TryCreateLexemes(document, conceptMap, out var lexemes, out error))
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

    internal sealed class ConceptDocument
    {
        public ConceptDocument() { }
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
    }

    internal sealed class LexemeDocument
    {
        public LexemeDocument() { }
        public string Form { get; set; } = string.Empty;
        public string Concept { get; set; } = string.Empty;
        public string PartOfSpeech { get; set; } = string.Empty;
    }

    internal sealed class RelationDocument
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

    internal sealed class TypeDefinitionDocument
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

    internal sealed class RestrictionDocument
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

    internal sealed class TypeAssertionDocument
    {
        public TypeAssertionDocument() { }
        public string Individual { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? SourceText { get; set; }
        public string? SourceUrl { get; set; }
    }

    internal sealed class RelationAssertionDocument
    {
        public RelationAssertionDocument() { }
        public string Subject { get; set; } = string.Empty;
        public string Relation { get; set; } = string.Empty;
        public string Object { get; set; } = string.Empty;
        public string? SourceText { get; set; }
        public string? SourceUrl { get; set; }
    }
}
