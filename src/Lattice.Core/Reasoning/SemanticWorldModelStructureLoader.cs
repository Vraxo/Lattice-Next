using System.Collections.Immutable;

namespace Lattice.Core.Reasoning;

internal static class SemanticWorldModelStructureLoader
{
    internal static bool TryCreateConcepts(
        SemanticWorldModelLoader.SemanticWorldModelDocument document,
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

    internal static bool TryCreateRelations(
        SemanticWorldModelLoader.SemanticWorldModelDocument document,
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

    internal static bool TryCreateAssertions(
        SemanticWorldModelLoader.SemanticWorldModelDocument document,
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

    internal static bool TryCreateLexemes(
        SemanticWorldModelLoader.SemanticWorldModelDocument document,
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
}
