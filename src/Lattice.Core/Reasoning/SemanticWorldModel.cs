using System.Collections.Immutable;

namespace Lattice.Core.Reasoning;

public enum SemanticConceptKind
{
    Type,
    Individual
}

public enum SemanticRelationArgumentKind
{
    Individual,
    Type
}

public sealed record SemanticConcept(
    string Id,
    string Label,
    SemanticConceptKind Kind);

public sealed record SemanticLexeme(
    string Form,
    string Concept,
    string PartOfSpeech)
{
    /// <summary>
    /// The parts of a phrase this form matches by name, such as an object's name or a light kind. A form with no
    /// slots matches the whole phrase and captures nothing.
    /// </summary>
    public ImmutableArray<SemanticLexemeSlot> Slots { get; init; } = ImmutableArray<SemanticLexemeSlot>.Empty;
}

/// <summary>
/// One named gap in a lexeme form. `Name` locates the `{name}` marker in the form, `Concept` is the individual
/// the capture fills, and `Kind` is what the capture must be: `text` keeps the phrase as written, any other kind
/// names a type the phrase must resolve to, so the value that fills the slot is that individual's label.
/// </summary>
public sealed record SemanticLexemeSlot(
    string Name,
    string Concept,
    string Kind);

public sealed record SemanticRelationDefinition(
    string Id,
    string? DomainType,
    string? RangeType,
    string Verb,
    string? ObjectRole,
    int Order,
    int ComparisonPriority,
    SemanticRelationArgumentKind SubjectKind = SemanticRelationArgumentKind.Individual,
    SemanticRelationArgumentKind ObjectKind = SemanticRelationArgumentKind.Individual);

public sealed record SemanticRestriction(
    string Relation,
    string TargetType);

public sealed record SemanticWorldModelSource(string SourceId);

public sealed record SemanticTypeDefinition(
    string Concept,
    ImmutableArray<SemanticRestriction> SufficientConditions,
    SemanticWorldModelSource Source)
{
    public SemanticDefinitionKind DefinitionKind { get; init; } = SemanticDefinitionKind.Primitive;
    public ImmutableArray<SemanticRestriction> NecessaryConditions { get; init; } = [];
    public SemanticClassExpression? SufficientExpression { get; init; }
}

public sealed record SemanticRelationAssertion(
    string Subject,
    string Relation,
    string Object,
    SemanticWorldModelSource Source);

public enum SemanticDataValueKind
{
    Uri,
    Number,
    Decimal
}

public sealed record SemanticDataPropertyDefinition(
    string Id,
    string DomainType,
    SemanticDataValueKind ValueKind,
    string? Dimension = null);

public sealed record SemanticDataAssertion(
    string Subject,
    string Property,
    string Value,
    SemanticWorldModelSource Source,
    string? Unit = null);

public sealed record SemanticWorldModel(
    int FormatVersion,
    string Id,
    string Name,
    ImmutableArray<SemanticConcept> Concepts,
    ImmutableArray<SemanticLexeme> Lexemes,
    ImmutableArray<SemanticRelationDefinition> Relations,
    ImmutableArray<SemanticTypeDefinition> Definitions,
    ImmutableArray<SemanticRelationAssertion> RelationAssertions,
    ImmutableArray<SemanticDataPropertyDefinition> DataProperties,
    ImmutableArray<SemanticDataAssertion> DataAssertions)
{
    /// <summary>
    /// Resolves a phrase to the concept its form names — exactly when a form or id matches the whole phrase, and
    /// otherwise through a form that declares slots — and reports the text each slot captured.
    /// </summary>
    public bool TryMatchLexeme(
        string? phrase,
        out SemanticConcept? concept,
        out SemanticLexeme? lexeme,
        out ImmutableDictionary<string, string> captures)
    {
        concept = null;
        lexeme = null;
        captures = ImmutableDictionary.Create<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(phrase))
        {
            return false;
        }

        var exact = FindConcept(phrase, SemanticConceptKind.Individual);
        if (exact is not null)
        {
            concept = exact;
            return true;
        }

        foreach (var candidate in Lexemes)
        {
            if (candidate.Slots.IsDefaultOrEmpty)
            {
                continue;
            }

            var pattern = new System.Text.StringBuilder("^");
            var slotIndex = 0;
            var position = 0;
            foreach (var slot in candidate.Slots)
            {
                var marker = "{" + slot.Name + "}";
                var at = candidate.Form.IndexOf(marker, position, StringComparison.Ordinal);
                if (at < 0)
                {
                    continue;
                }

                var literal = candidate.Form[position..at].Trim();
                if (literal.Length > 0)
                {
                    if (pattern.Length > 1)
                    {
                        pattern.Append("\\s+");
                    }

                    pattern.Append(System.Text.RegularExpressions.Regex.Escape(literal));
                    pattern.Append("\\s+");
                }

                pattern.Append("(?<s").Append(slotIndex).Append(">.+?)");
                position = at + marker.Length;
                slotIndex++;
            }

            if (position == 0)
            {
                continue;
            }

            var tail = candidate.Form[position..].Trim();
            if (tail.Length > 0)
            {
                pattern.Append("\\s+").Append(System.Text.RegularExpressions.Regex.Escape(tail));
            }

            pattern.Append('$');
            var match = System.Text.RegularExpressions.Regex.Match(
                phrase.Trim(),
                pattern.ToString(),
                System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                continue;
            }

            var captured = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
            for (var index = 0; index < slotIndex; index++)
            {
                captured[candidate.Slots[index].Name] = match.Groups["s" + index].Value.Trim();
            }

            var target = Concepts.FirstOrDefault(item =>
                item.Id == candidate.Concept && item.Kind == SemanticConceptKind.Individual);
            if (target is null)
            {
                continue;
            }

            concept = target;
            lexeme = candidate;
            captures = captured.ToImmutable();
            return true;
        }

        return false;
    }

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
