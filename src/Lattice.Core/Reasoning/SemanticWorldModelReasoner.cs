using System.Collections.Immutable;

namespace Lattice.Core.Reasoning;

public sealed class SemanticWorldModelReasoner
{
    private const string IsA = "is-a";
    private const string InstanceOf = "instance-of";

    private readonly SemanticWorldModel model;
    private readonly ImmutableDictionary<string, ImmutableArray<string>> typeClosure;

    public SemanticWorldModelReasoner(SemanticWorldModel model)
    {
        this.model = model ?? throw new ArgumentNullException(nameof(model));
        typeClosure = BuildTypeClosure(this.model);
    }

    public bool IsInstanceOf(string individual, string type) =>
        GetTypes(individual).Contains(SemanticWorldModel.Normalize(type));

    public ImmutableArray<string> GetTypes(string individual)
    {
        var normalized = SemanticWorldModel.Normalize(individual);
        return typeClosure.TryGetValue(normalized, out var types)
            ? types
            : ImmutableArray<string>.Empty;
    }

    public ImmutableArray<string> GetDirectSupertypes(string type)
    {
        var normalized = SemanticWorldModel.Normalize(type);
        return model.RelationAssertions
            .Where(assertion => assertion.Subject == normalized && assertion.Relation == IsA)
            .Select(assertion => assertion.Object)
            .Distinct(StringComparer.Ordinal)
            .ToImmutableArray();
    }

    public ImmutableArray<string> GetDirectSubtypes(string type)
    {
        var normalized = SemanticWorldModel.Normalize(type);
        return model.RelationAssertions
            .Where(assertion => assertion.Object == normalized && assertion.Relation == IsA)
            .Select(assertion => assertion.Subject)
            .Distinct(StringComparer.Ordinal)
            .ToImmutableArray();
    }

    public bool IsSubtypeOf(string candidate, string expected) =>
        IsSubtype(model, SemanticWorldModel.Normalize(candidate), SemanticWorldModel.Normalize(expected));

    private static ImmutableDictionary<string, ImmutableArray<string>> BuildTypeClosure(
        SemanticWorldModel model)
    {
        var allTypes = model.Concepts
            .Where(concept => concept.Kind == SemanticConceptKind.Individual)
            .ToDictionary(
                concept => concept.Id,
                _ => new HashSet<string>(StringComparer.Ordinal),
                StringComparer.Ordinal);

        foreach (var assertion in model.RelationAssertions.Where(assertion =>
            assertion.Relation == InstanceOf))
        {
            allTypes[assertion.Subject].Add(assertion.Object);
        }

        var changed = true;
        var passes = 0;
        var maximumPasses = model.Definitions.Length + model.Concepts.Length;
        while (changed && passes++ < maximumPasses)
        {
            changed = false;
            foreach (var individualId in allTypes.Keys)
            {
                foreach (var currentType in allTypes[individualId].ToArray())
                {
                    foreach (var parent in model.RelationAssertions
                        .Where(assertion => assertion.Subject == currentType && assertion.Relation == IsA)
                        .Select(assertion => assertion.Object))
                    {
                        changed |= allTypes[individualId].Add(parent);
                    }
                }

                foreach (var definition in model.Definitions)
                {
                    if (allTypes[individualId].Contains(definition.Concept) ||
                        definition.DefinitionKind != SemanticDefinitionKind.Defined ||
                        definition.SufficientExpression is null)
                    {
                        continue;
                    }

                    if (MatchesExpression(definition.SufficientExpression, individualId, allTypes, model))
                    {
                        changed |= allTypes[individualId].Add(definition.Concept);
                    }
                }
            }
        }

        return allTypes.ToImmutableDictionary(
            pair => pair.Key,
            pair => pair.Value.Order(StringComparer.Ordinal).ToImmutableArray(),
            StringComparer.Ordinal);
    }

    private static bool MatchesExpression(
        SemanticClassExpression expression,
        string individual,
        IReadOnlyDictionary<string, HashSet<string>> allTypes,
        SemanticWorldModel model)
    {
        return expression.Kind switch
        {
            SemanticClassExpressionKind.Some => model.RelationAssertions.Any(assertion =>
                assertion.Subject == individual &&
                assertion.Relation == expression.Relation &&
                allTypes.TryGetValue(assertion.Object, out var objectTypes) &&
                objectTypes.Contains(expression.TargetType!) &&
                (expression.Filler is null ||
                 MatchesExpression(expression.Filler, assertion.Object, allTypes, model))),
            SemanticClassExpressionKind.And => expression.Operands.All(operand =>
                MatchesExpression(operand, individual, allTypes, model)),
            SemanticClassExpressionKind.Or => expression.Operands.Any(operand =>
                MatchesExpression(operand, individual, allTypes, model)),
            SemanticClassExpressionKind.Cycle => TryFindCycleAssertions(
                individual, expression, allTypes, model, out _),
            _ => false
        };
    }


    private static bool TryFindCycleAssertions(
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
                foreach (var edge in model.RelationAssertions.Where(assertion =>
                    assertion.Subject == current &&
                    assertion.Relation == expression.Relation &&
                    allTypes.TryGetValue(assertion.Object, out var types) &&
                    types.Contains(expression.TargetType!)))
                {
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
        }

        return false;
    }

    public ImmutableArray<SemanticWorldModelMembership> GetMemberships(string individual)
    {
        var concept = model.FindConcept(individual, SemanticConceptKind.Individual);
        if (concept is null)
        {
            return ImmutableArray<SemanticWorldModelMembership>.Empty;
        }

        return GetTypes(concept.Id)
            .Select(type => model.FindConcept(type, SemanticConceptKind.Type))
            .Where(type => type is not null)
            .Select(type => new SemanticWorldModelMembership(
                concept,
                type!,
                GetMembershipEvidence(concept.Id, type!.Id)))
            .ToImmutableArray();
    }

    public ImmutableArray<SemanticWorldModelSource> GetMembershipEvidence(
        string individual,
        string type)
    {
        var normalizedIndividual = SemanticWorldModel.Normalize(individual);
        var normalizedType = SemanticWorldModel.Normalize(type);
        var evidence = new List<SemanticWorldModelSource>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        AppendMembershipEvidence(normalizedIndividual, normalizedType, visited, evidence);
        return evidence.Distinct().ToImmutableArray();
    }

    public ImmutableArray<SemanticRelationAssertion> GetRelations(
        string subject,
        string? relation = null)
    {
        var normalizedSubject = SemanticWorldModel.Normalize(subject);
        var normalizedRelation = relation is null ? null : SemanticWorldModel.Normalize(relation);

        return model.RelationAssertions
            .Where(assertion =>
                assertion.Subject == normalizedSubject &&
                (normalizedRelation is null || assertion.Relation == normalizedRelation))
            .OrderBy(assertion => model.FindRelation(assertion.Relation)?.Order ?? 0)
            .ThenBy(assertion => assertion.Relation, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    public SemanticWorldModel? Model => model;

    private void AppendMembershipEvidence(
        string individual,
        string type,
        HashSet<string> visited,
        List<SemanticWorldModelSource> evidence)
    {
        if (!visited.Add($"{individual}:{type}"))
        {
            return;
        }

        foreach (var assertion in model.RelationAssertions.Where(assertion =>
            assertion.Subject == individual &&
            assertion.Relation == InstanceOf &&
            IsSubtype(model, assertion.Object, type)))
        {
            evidence.Add(assertion.Source);
            AddTypeHierarchySources(assertion.Object, type, evidence);
        }

        foreach (var definition in model.Definitions.Where(definition =>
            definition.Concept == type ||
            IsSubtype(model, definition.Concept, type)))
        {
            if (definition.SufficientExpression is null)
            {
                continue;
            }

            var matchedSources = new List<SemanticWorldModelSource>();
            if (TryAppendExpressionEvidence(
                individual,
                definition.SufficientExpression,
                visited,
                matchedSources))
            {
                evidence.Add(definition.Source);
                AddTypeHierarchySources(definition.Concept, type, evidence);
                evidence.AddRange(matchedSources);
            }
        }

        foreach (var childType in GetDirectSubtypes(type))
        {
            var hierarchyAssertion = model.RelationAssertions.FirstOrDefault(assertion =>
                assertion.Subject == childType && assertion.Relation == IsA && assertion.Object == type);
            if (hierarchyAssertion is not null && IsInstanceOf(individual, childType))
            {
                evidence.Add(hierarchyAssertion.Source);
                AppendMembershipEvidence(individual, childType, visited, evidence);
            }
        }
    }

    private bool MatchesExpressionForEvidence(
        string individual,
        SemanticClassExpression expression)
    {
        return expression.Kind switch
        {
            SemanticClassExpressionKind.Some => model.RelationAssertions.Any(assertion =>
                assertion.Subject == individual &&
                assertion.Relation == expression.Relation &&
                IsInstanceOf(assertion.Object, expression.TargetType!) &&
                (expression.Filler is null ||
                 MatchesExpressionForEvidence(assertion.Object, expression.Filler))),
            SemanticClassExpressionKind.And => expression.Operands.All(operand =>
                MatchesExpressionForEvidence(individual, operand)),
            SemanticClassExpressionKind.Or => expression.Operands.Any(operand =>
                MatchesExpressionForEvidence(individual, operand)),
            _ => false
        };
    }

    private bool TryAppendExpressionEvidence(
        string individual,
        SemanticClassExpression expression,
        HashSet<string> visited,
        List<SemanticWorldModelSource> evidence)
    {
        if (expression.Kind == SemanticClassExpressionKind.Cycle)
        {
            if (!TryFindCycleAssertions(
                individual,
                expression,
                model.Concepts
                    .Where(concept => concept.Kind == SemanticConceptKind.Individual)
                    .ToDictionary(
                        concept => concept.Id,
                        concept => GetTypes(concept.Id).ToHashSet(StringComparer.Ordinal),
                        StringComparer.Ordinal),
                model,
                out var cycleAssertions))
            {
                return false;
            }

            evidence.AddRange(cycleAssertions.Select(assertion => assertion.Source));
            foreach (var node in cycleAssertions
                .Skip(1)
                .Select(assertion => assertion.Object)
                .Distinct(StringComparer.Ordinal))
            {
                AppendMembershipEvidence(node, expression.TargetType!, visited, evidence);
            }

            return true;
        }

        if (expression.Kind == SemanticClassExpressionKind.Some)
        {
            var assertion = model.RelationAssertions.FirstOrDefault(candidate =>
                candidate.Subject == individual &&
                candidate.Relation == expression.Relation &&
                IsInstanceOf(candidate.Object, expression.TargetType!) &&
                (expression.Filler is null ||
                 MatchesExpressionForEvidence(candidate.Object, expression.Filler)));
            if (assertion is null)
            {
                return false;
            }

            evidence.Add(assertion.Source);
            AppendMembershipEvidence(assertion.Object, expression.TargetType!, visited, evidence);
            if (expression.Filler is not null)
            {
                TryAppendExpressionEvidence(assertion.Object, expression.Filler, visited, evidence);
            }

            return true;
        }

        if (expression.Kind == SemanticClassExpressionKind.Or)
        {
            foreach (var operand in expression.Operands)
            {
                var branchEvidence = new List<SemanticWorldModelSource>();
                if (TryAppendExpressionEvidence(individual, operand, visited, branchEvidence))
                {
                    evidence.AddRange(branchEvidence);
                    return true;
                }
            }

            return false;
        }

        var conjunctiveEvidence = new List<SemanticWorldModelSource>();
        foreach (var operand in expression.Operands)
        {
            if (!TryAppendExpressionEvidence(individual, operand, visited, conjunctiveEvidence))
            {
                return false;
            }
        }

        evidence.AddRange(conjunctiveEvidence);
        return true;
    }

    private void AddTypeHierarchySources(
        string candidate,
        string expected,
        List<SemanticWorldModelSource> evidence)
    {
        var pending = new Stack<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        pending.Push(candidate);

        while (pending.TryPop(out var current))
        {
            if (!visited.Add(current) || current == expected)
            {
                continue;
            }

            foreach (var assertion in model.RelationAssertions.Where(assertion =>
                assertion.Subject == current && assertion.Relation == IsA))
            {
                evidence.Add(assertion.Source);
                pending.Push(assertion.Object);
            }
        }
    }

    private static bool IsSubtype(
        SemanticWorldModel model,
        string candidate,
        string expected)
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
                .Where(assertion => assertion.Subject == current && assertion.Relation == IsA)
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
}
