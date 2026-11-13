using System.Collections.Immutable;

namespace Lattice.Core.Reasoning;

public sealed class SemanticWorldModelReasoner
{
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

    private static ImmutableDictionary<string, ImmutableArray<string>> BuildTypeClosure(
        SemanticWorldModel model)
    {
        var allTypes = model.Concepts
            .Where(concept => concept.Kind == SemanticConceptKind.Individual)
            .ToDictionary(
                concept => concept.Id,
                _ => new HashSet<string>(StringComparer.Ordinal),
                StringComparer.Ordinal);

        foreach (var assertion in model.TypeAssertions)
        {
            allTypes[assertion.Individual].Add(assertion.Type);
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
                    foreach (var parent in model.FindDefinition(currentType)?.ParentTypes ?? [])
                    {
                        changed |= allTypes[individualId].Add(parent);
                    }
                }

                foreach (var definition in model.Definitions)
                {
                    if (allTypes[individualId].Contains(definition.Concept) ||
                        definition.DefinitionKind != SemanticDefinitionKind.Defined ||
                        definition.EquivalentExpression is null ||
                        !definition.ParentTypes.All(parent => allTypes[individualId].Contains(parent)))
                    {
                        continue;
                    }

                    if (MatchesExpression(definition.EquivalentExpression, individualId, allTypes, model))
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
                objectTypes.Contains(expression.TargetType!)),
            SemanticClassExpressionKind.And => expression.Operands.All(operand =>
                MatchesExpression(operand, individual, allTypes, model)),
            SemanticClassExpressionKind.Or => expression.Operands.Any(operand =>
                MatchesExpression(operand, individual, allTypes, model)),
            _ => false
        };
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

        foreach (var assertion in model.TypeAssertions.Where(assertion =>
            assertion.Individual == individual &&
            IsSubtype(assertion.Type, type)))
        {
            evidence.Add(assertion.Source);
            AddTypeHierarchySources(assertion.Type, type, evidence);
        }

        foreach (var definition in model.Definitions.Where(definition =>
            definition.Concept == type ||
            IsSubtype(definition.Concept, type)))
        {
            if (definition.EquivalentConditions.IsDefaultOrEmpty)
            {
                continue;
            }

            var matchedSources = new List<SemanticWorldModelSource>();
            var matched = true;

            foreach (var condition in definition.EquivalentConditions)
            {
                var relationAssertion = model.RelationAssertions.FirstOrDefault(assertion =>
                    assertion.Subject == individual &&
                    assertion.Relation == condition.Relation &&
                    IsInstanceOf(assertion.Object, condition.TargetType));

                if (relationAssertion is null)
                {
                    matched = false;
                    break;
                }

                matchedSources.Add(relationAssertion.Source);
                AppendMembershipEvidence(
                    relationAssertion.Object,
                    condition.TargetType,
                    visited,
                    matchedSources);
            }

            if (matched)
            {
                evidence.Add(definition.Source);
                evidence.AddRange(matchedSources);
            }
        }

        var inheritedDefinitions = model.Definitions
            .Where(definition => definition.ParentTypes.Contains(type, StringComparer.Ordinal));

        foreach (var definition in inheritedDefinitions)
        {
            if (IsInstanceOf(individual, definition.Concept))
            {
                AppendMembershipEvidence(individual, definition.Concept, visited, evidence);
            }
        }
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

            var definition = model.FindDefinition(current);
            if (definition is null)
            {
                continue;
            }

            evidence.Add(definition.Source);
            foreach (var parent in definition.ParentTypes)
            {
                pending.Push(parent);
            }
        }
    }

    private bool IsSubtype(string candidate, string expected)
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

            foreach (var parent in model.FindDefinition(current)?.ParentTypes ?? [])
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
