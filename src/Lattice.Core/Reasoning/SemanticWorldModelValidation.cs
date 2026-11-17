using System.Collections.Immutable;

namespace Lattice.Core.Reasoning;

internal static class SemanticWorldModelValidation
{
    internal static bool AreAssertionsWellTyped(SemanticWorldModel model)
    {
        var reasoner = new SemanticWorldModelReasoner(model);
        foreach (var assertion in model.RelationAssertions)
        {
            var relation = model.FindRelation(assertion.Relation)!;
            if ((relation.SubjectKind == SemanticRelationArgumentKind.Individual &&
                    (model.FindConcept(assertion.Subject, SemanticConceptKind.Individual) is null ||
                     (relation.DomainType is not null &&
                      !reasoner.IsInstanceOf(assertion.Subject, relation.DomainType)))) ||
                (relation.SubjectKind == SemanticRelationArgumentKind.Type &&
                    model.FindConcept(assertion.Subject, SemanticConceptKind.Type) is null) ||
                (relation.ObjectKind == SemanticRelationArgumentKind.Individual &&
                    (model.FindConcept(assertion.Object, SemanticConceptKind.Individual) is null ||
                     (relation.RangeType is not null &&
                      !reasoner.IsInstanceOf(assertion.Object, relation.RangeType)))) ||
                (relation.ObjectKind == SemanticRelationArgumentKind.Type &&
                    model.FindConcept(assertion.Object, SemanticConceptKind.Type) is null))
            {
                return false;
            }
        }

        foreach (var definition in model.Definitions)
        {
            foreach (var condition in definition.NecessaryConditions)
            {
                var relation = model.FindRelation(condition.Relation)!;
                if (relation.DomainType is null ||
                    relation.RangeType is null ||
                    !IsSubtype(model, definition.Concept, relation.DomainType) ||
                    !IsSubtype(model, condition.TargetType, relation.RangeType))
                {
                    return false;
                }
            }

            if (definition.SufficientExpression is not null &&
                !IsExpressionWellTyped(definition.SufficientExpression, definition.Concept, model))
            {
                return false;
            }
        }

        foreach (var assertion in model.DataAssertions)
        {
            var property = model.FindDataProperty(assertion.Property)!;
            if (!reasoner.IsInstanceOf(assertion.Subject, property.DomainType))
            {
                return false;
            }
        }

        return true;
    }

    internal static bool IsValidConnectionRelation(
        string? relationId,
        IReadOnlyDictionary<string, SemanticRelationDefinition> relations) =>
        !string.IsNullOrWhiteSpace(relationId) &&
        relations.TryGetValue(relationId, out var relation) &&
        relation.SubjectKind == SemanticRelationArgumentKind.Individual &&
        relation.ObjectKind == SemanticRelationArgumentKind.Individual &&
        relation.DomainType is not null &&
        relation.RangeType is not null;

    private static bool IsExpressionWellTyped(
        SemanticClassExpression expression,
        string subjectType,
        SemanticWorldModel model)
    {
        if (expression.Kind is SemanticClassExpressionKind.Some or SemanticClassExpressionKind.All)
        {
            var relation = model.FindRelation(expression.Relation!)!;
            return relation.DomainType is not null &&
                relation.RangeType is not null &&
                IsSubtype(model, subjectType, relation.DomainType) &&
                IsSubtype(model, expression.TargetType!, relation.RangeType) &&
                (expression.Filler is null ||
                 IsExpressionWellTyped(expression.Filler, expression.TargetType!, model));
        }

        if (expression.Kind == SemanticClassExpressionKind.Cycle)
        {
            var startRelation = model.FindRelation(expression.StartRelation!)!;
            var traversalRelation = model.FindRelation(expression.Relation!)!;
            return startRelation.DomainType is not null &&
                startRelation.RangeType is not null &&
                traversalRelation.DomainType is not null &&
                traversalRelation.RangeType is not null &&
                IsSubtype(model, subjectType, startRelation.DomainType) &&
                IsSubtype(model, expression.TargetType!, startRelation.RangeType) &&
                IsSubtype(model, expression.TargetType!, traversalRelation.DomainType) &&
                IsSubtype(model, expression.TargetType!, traversalRelation.RangeType) &&
                AreConnectionRelationsWellTyped(expression, model);
        }

        return expression.Kind is SemanticClassExpressionKind.And or SemanticClassExpressionKind.Or &&
            expression.Operands.All(operand =>
                IsExpressionWellTyped(operand, subjectType, model));
    }

    private static bool AreConnectionRelationsWellTyped(
        SemanticClassExpression expression,
        SemanticWorldModel model)
    {
        if (expression.SourceConnectionRelation is null &&
            expression.TargetConnectionRelation is null)
        {
            return true;
        }

        if (expression.SourceConnectionRelation is null ||
            expression.TargetConnectionRelation is null)
        {
            return false;
        }

        var source = model.FindRelation(expression.SourceConnectionRelation)!;
        var target = model.FindRelation(expression.TargetConnectionRelation)!;
        return source.DomainType is not null &&
            source.RangeType is not null &&
            target.DomainType is not null &&
            target.RangeType is not null &&
            IsSubtype(model, expression.TargetType!, source.DomainType) &&
            IsSubtype(model, expression.TargetType!, target.DomainType) &&
            (IsSubtype(model, source.RangeType, target.RangeType) ||
             IsSubtype(model, target.RangeType, source.RangeType));
    }

    private static bool IsSubtype(SemanticWorldModel model, string candidate, string expected)
    {
        if (candidate == expected)
        {
            return true;
        }

        var pending = new Stack<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        pending.Push(candidate);

        while (pending.TryPop(out var current))
        {
            if (!visited.Add(current))
            {
                continue;
            }

            foreach (var parent in model.RelationAssertions
                .Where(assertion => assertion.Subject == current && assertion.Relation == "is-a")
                .Select(assertion => assertion.Object))
            {
                if (parent == expected)
                {
                    return true;
                }

                pending.Push(parent);
            }
        }

        return false;
    }

    internal static bool HasInheritanceCycle(
        ImmutableArray<SemanticRelationAssertion> assertions)
    {
        var parentsByType = assertions
            .Where(assertion => assertion.Relation == "is-a")
            .GroupBy(assertion => assertion.Subject, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(assertion => assertion.Object).ToArray(),
                StringComparer.Ordinal);
        var completed = new HashSet<string>(StringComparer.Ordinal);
        var active = new HashSet<string>(StringComparer.Ordinal);

        bool Visit(string type)
        {
            if (completed.Contains(type))
            {
                return false;
            }

            if (!active.Add(type))
            {
                return true;
            }

            if (parentsByType.TryGetValue(type, out var parents))
            {
                foreach (var parent in parents)
                {
                    if (Visit(parent))
                    {
                        return true;
                    }
                }
            }

            active.Remove(type);
            completed.Add(type);
            return false;
        }

        return parentsByType.Keys.Any(Visit);
    }

    internal static bool IsRelationArgument(
        IReadOnlyDictionary<string, SemanticConcept> concepts,
        string? id,
        SemanticRelationArgumentKind kind) =>
        kind switch
        {
            SemanticRelationArgumentKind.Individual => IsIndividual(concepts, id),
            SemanticRelationArgumentKind.Type => IsType(concepts, id),
            _ => false
        };

    internal static bool IsType(IReadOnlyDictionary<string, SemanticConcept> concepts, string? id) =>
        id is not null &&
        concepts.TryGetValue(id, out var concept) &&
        concept.Kind == SemanticConceptKind.Type;

    internal static bool IsIndividual(IReadOnlyDictionary<string, SemanticConcept> concepts, string? id) =>
        id is not null &&
        concepts.TryGetValue(id, out var concept) &&
        concept.Kind == SemanticConceptKind.Individual;

    internal static bool IsValidId(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 80 &&
        value[0] is >= 'a' and <= 'z' &&
        value.All(character =>
            character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');
}
