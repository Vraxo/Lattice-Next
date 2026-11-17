using System.Collections.Immutable;

namespace Lattice.Core.Reasoning;

internal static class SemanticWorldModelDefinitionLoader
{
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
                !SemanticWorldModelValidation.SemanticWorldModelValidation.IsType(concepts, expression.TargetType) ||
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
                SemanticWorldModelValidation.SemanticWorldModelValidation.IsType(concepts, expression.TargetType) &&
                expression.MinimumLength is >= 2 and <= 64 &&
                ((expression.SourceConnectionRelation is null &&
                  expression.TargetConnectionRelation is null) ||
                 (SemanticWorldModelValidation.SemanticWorldModelValidation.IsValidConnectionRelation(expression.SourceConnectionRelation, relations) &&
                  SemanticWorldModelValidation.SemanticWorldModelValidation.IsValidConnectionRelation(expression.TargetConnectionRelation, relations))) &&
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

    private static bool TryCreateRestrictions(
        string ownerType,
        IReadOnlyCollection<SemanticWorldModelLoader.RestrictionDocument> documents,
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
                !SemanticWorldModelValidation.SemanticWorldModelValidation.IsType(concepts, condition.TargetType))
            {
                error = $"Type definition '{ownerType}' contains an invalid necessary or sufficient relation restriction.";
                return false;
            }

            restrictions.Add(new SemanticRestriction(condition.Relation, condition.TargetType));
        }

        return true;
    }

    private static bool TryCreateDefinitions(
        SemanticWorldModelLoader.SemanticWorldModelDocument document,
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
                parents.All(parent => SemanticWorldModelValidation.SemanticWorldModelValidation.IsType(concepts, parent)) &&
                parents.Distinct(StringComparer.Ordinal).Count() == parents.Count;

            if (item is null ||
                !SemanticWorldModelValidation.SemanticWorldModelValidation.IsType(concepts, item.Concept) ||
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
}
