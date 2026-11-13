using System.Collections.Immutable;

namespace Lattice.Core.Reasoning;

public sealed class SemanticWorldModelReasoner
{
    private readonly SemanticWorldModel model;

    public SemanticWorldModelReasoner(SemanticWorldModel model)
    {
        this.model = model ?? throw new ArgumentNullException(nameof(model));
    }

    public bool IsInstanceOf(string individual, string type) =>
        GetTypes(individual).Contains(SemanticWorldModel.Normalize(type));

    public ImmutableArray<string> GetTypes(string individual)
    {
        var normalized = SemanticWorldModel.Normalize(individual);
        if (model.FindConcept(normalized, SemanticConceptKind.Individual) is null)
        {
            return ImmutableArray<string>.Empty;
        }

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
        while (changed && passes++ < model.Definitions.Length + model.Concepts.Length)
        {
            changed = false;
            foreach (var individual in allTypes.Keys)
            {
                foreach (var currentType in allTypes[individual].ToArray())
                {
                    foreach (var parent in model.FindDefinition(currentType)?.ParentTypes ?? [])
                    {
                        changed |= allTypes[individual].Add(parent);
                    }
                }

                foreach (var definition in model.Definitions)
                {
                    if (allTypes[individual].Contains(definition.Concept) ||
                        definition.EquivalentConditions.IsDefaultOrEmpty)
                    {
                        continue;
                    }

                    var conditionsSatisfied = definition.EquivalentConditions.All(condition =>
                        model.RelationAssertions.Any(assertion =>
                            assertion.Subject == individual &&
                            assertion.Relation == condition.Relation &&
                            allTypes.TryGetValue(assertion.Object, out var objectTypes) &&
                            objectTypes.Contains(condition.TargetType)));

                    if (conditionsSatisfied)
                    {
                        changed |= allTypes[individual].Add(definition.Concept);
                    }
                }
            }
        }

        return allTypes[normalized]
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();
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
