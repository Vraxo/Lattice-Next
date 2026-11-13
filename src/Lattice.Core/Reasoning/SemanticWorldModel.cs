using System.Collections.Immutable;

namespace Lattice.Core.Reasoning;

public enum SemanticConceptKind
{
    Type,
    Individual
}

public sealed record SemanticConcept(
    string Id,
    string Label,
    SemanticConceptKind Kind);

public sealed record SemanticLexeme(
    string Form,
    string Concept,
    string PartOfSpeech);

public sealed record SemanticRelationDefinition(
    string Id,
    string DomainType,
    string RangeType,
    string Verb,
    string? ObjectRole,
    int Order,
    int ComparisonPriority);

public sealed record SemanticRestriction(
    string Relation,
    string TargetType);

public sealed record SemanticTypeDefinition(
    string Concept,
    ImmutableArray<string> ParentTypes,
    ImmutableArray<SemanticRestriction> EquivalentConditions,
    SemanticWorldModelSource Source);

public sealed record SemanticWorldModelSource(
    string SourceText,
    string? SourceUrl,
    string SourceId);

public sealed record SemanticTypeAssertion(
    string Individual,
    string Type,
    SemanticWorldModelSource Source);

public sealed record SemanticRelationAssertion(
    string Subject,
    string Relation,
    string Object,
    SemanticWorldModelSource Source);

public enum SemanticDataValueKind
{
    Uri
}

public sealed record SemanticDataPropertyDefinition(
    string Id,
    string DomainType,
    SemanticDataValueKind ValueKind);

public sealed record SemanticDataAssertion(
    string Subject,
    string Property,
    string Value,
    SemanticWorldModelSource Source);

public sealed record SemanticWorldModel(
    int FormatVersion,
    string Id,
    string Name,
    ImmutableArray<SemanticConcept> Concepts,
    ImmutableArray<SemanticLexeme> Lexemes,
    ImmutableArray<SemanticRelationDefinition> Relations,
    ImmutableArray<SemanticTypeDefinition> Definitions,
    ImmutableArray<SemanticTypeAssertion> TypeAssertions,
    ImmutableArray<SemanticRelationAssertion> RelationAssertions,
    ImmutableArray<SemanticDataPropertyDefinition> DataProperties,
    ImmutableArray<SemanticDataAssertion> DataAssertions)
{
    public SemanticConcept? FindConcept(string value, SemanticConceptKind? kind = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = Normalize(value);
        var direct = Concepts.FirstOrDefault(concept =>
            concept.Id == normalized && (kind is null || concept.Kind == kind));

        if (direct is not null)
        {
            return direct;
        }

        var lexical = Lexemes.FirstOrDefault(lexeme =>
            Normalize(lexeme.Form) == normalized);
        return lexical is null
            ? null
            : Concepts.FirstOrDefault(concept =>
                concept.Id == lexical.Concept && (kind is null || concept.Kind == kind));
    }

    public SemanticRelationDefinition? FindRelation(string id) =>
        Relations.FirstOrDefault(relation => relation.Id == Normalize(id));

    public SemanticTypeDefinition? FindDefinition(string type) =>
        Definitions.FirstOrDefault(definition => definition.Concept == Normalize(type));

    public SemanticDataPropertyDefinition? FindDataProperty(string id) =>
        DataProperties.FirstOrDefault(property => property.Id == Normalize(id));

    public ImmutableArray<SemanticDataAssertion> GetDataAssertions(
        string subject,
        string? property = null)
    {
        var normalizedSubject = Normalize(subject);
        var normalizedProperty = property is null ? null : Normalize(property);

        return DataAssertions
            .Where(assertion =>
                assertion.Subject == normalizedSubject &&
                (normalizedProperty is null || assertion.Property == normalizedProperty))
            .ToImmutableArray();
    }

    public static string Normalize(string value) =>
        string.Join(
            '-',
            value.Trim()
                .Replace('_', ' ')
                .Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();
}

public sealed record SemanticWorldModelMembership(
    SemanticConcept Individual,
    SemanticConcept Type,
    ImmutableArray<SemanticWorldModelSource> Evidence);

