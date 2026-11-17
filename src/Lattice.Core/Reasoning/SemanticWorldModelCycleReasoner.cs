using System.Collections.Immutable;

namespace Lattice.Core.Reasoning;

internal static class SemanticWorldModelCycleReasoner
{
    internal static bool TryFindCycleAssertions(
        string individual,
        SemanticClassExpression expression,
        IReadOnlyDictionary<string, HashSet<string>> allTypes,
        SemanticWorldModel model,
        out ImmutableArray<SemanticRelationAssertion> cycleAssertions)
    {
        cycleAssertions = [];
        var startAssertions = model.RelationAssertions
            .Where(assertion =>
                assertion.Subject == individual &&
                assertion.Relation == expression.StartRelation &&
                allTypes.TryGetValue(assertion.Object, out var types) &&
                types.Contains(expression.TargetType!))
            .ToArray();

        foreach (var startAssertion in startAssertions)
        {
            var start = startAssertion.Object;
            var visited = new HashSet<string>(StringComparer.Ordinal) { start };
            var path = new List<SemanticRelationAssertion>();

            if (FindPath(start, start))
            {
                cycleAssertions = [startAssertion, .. path];
                return true;
            }

            bool FindPath(string current, string target)
            {
                if (path.Count >= 64)
                {
                    return false;
                }

                foreach (var edge in model.RelationAssertions.Where(assertion =>
                    assertion.Subject == current &&
                    assertion.Relation == expression.Relation &&
                    allTypes.TryGetValue(assertion.Object, out var types) &&
                    types.Contains(expression.TargetType!)))
                {
                    if (!HasMatchingConnection(current, edge.Object))
                    {
                        continue;
                    }

                    if (edge.Object == target)
                    {
                        if (path.Count + 1 >= expression.MinimumLength)
                        {
                            path.Add(edge);
                            return true;
                        }

                        continue;
                    }

                    if (!visited.Add(edge.Object))
                    {
                        continue;
                    }

                    path.Add(edge);
                    if (FindPath(edge.Object, target))
                    {
                        return true;
                    }

                    path.RemoveAt(path.Count - 1);
                    visited.Remove(edge.Object);
                }

                return false;
            }

            bool HasMatchingConnection(string source, string destination)
            {
                if (expression.SourceConnectionRelation is null &&
                    expression.TargetConnectionRelation is null)
                {
                    return true;
                }

                var sourceConnections = model.RelationAssertions
                    .Where(assertion =>
                        assertion.Subject == source &&
                        assertion.Relation == expression.SourceConnectionRelation)
                    .Select(assertion => assertion.Object)
                    .ToHashSet(StringComparer.Ordinal);

                return model.RelationAssertions.Any(assertion =>
                    assertion.Subject == destination &&
                    assertion.Relation == expression.TargetConnectionRelation &&
                    sourceConnections.Contains(assertion.Object));
            }

        }

        return false;
    }

    internal static ImmutableArray<SemanticRelationAssertion> GetCycleConnectionAssertions(
        ImmutableArray<SemanticRelationAssertion> cycleAssertions,
        SemanticClassExpression expression,
        SemanticWorldModel model)
    {
        if (expression.SourceConnectionRelation is null ||
            expression.TargetConnectionRelation is null)
        {
            return [];
        }

        var connections = ImmutableArray.CreateBuilder<SemanticRelationAssertion>();
        foreach (var edge in cycleAssertions.Where(assertion =>
            assertion.Relation == expression.Relation))
        {
            var sourceConnection = model.RelationAssertions.FirstOrDefault(assertion =>
                assertion.Subject == edge.Subject &&
                assertion.Relation == expression.SourceConnectionRelation &&
                model.RelationAssertions.Any(targetAssertion =>
                    targetAssertion.Subject == edge.Object &&
                    targetAssertion.Relation == expression.TargetConnectionRelation &&
                    targetAssertion.Object == assertion.Object));
            if (sourceConnection is null)
            {
                continue;
            }

            var targetConnection = model.RelationAssertions.First(assertion =>
                assertion.Subject == edge.Object &&
                assertion.Relation == expression.TargetConnectionRelation &&
                assertion.Object == sourceConnection.Object);
            connections.Add(sourceConnection);
            connections.Add(targetConnection);
        }

        return connections.Distinct().ToImmutableArray();
    }

    internal static ImmutableArray<SemanticWorldModelSource> GetCycleEvidence(
        SemanticWorldModel model,
        ImmutableDictionary<string, ImmutableArray<string>> typeClosure,
        string individual,
        SemanticClassExpression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
                if (expression.Kind != SemanticClassExpressionKind.Cycle)
                {
                    return [];
                }
        
                var normalized = SemanticWorldModel.Normalize(individual);
                var types = typeClosure.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value.ToHashSet(StringComparer.Ordinal),
                    StringComparer.Ordinal);
                if (!SemanticWorldModelCycleReasoner.TryFindCycleAssertions(normalized, expression, types, model, out var assertions))
                {
                    return [];
                }
        
                return assertions
                    .Concat(SemanticWorldModelCycleReasoner.GetCycleConnectionAssertions(assertions, expression, model))
                    .Select(assertion => assertion.Source)
                    .Distinct()
                    .ToImmutableArray();
    }
}
