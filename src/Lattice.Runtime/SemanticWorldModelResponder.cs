using System.Collections.Immutable;
using Lattice.Client;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

namespace Lattice.Runtime;

internal static class SemanticWorldModelResponder
{
    internal static bool TryRespond(
        SemanticWorldModelQuestionFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(reasoner);

        return request.Kind switch
        {
            SemanticWorldModelQuestionKind.Definition =>
                TryDefinition(request.Concept ?? request.Subject, model, out message, out evidence),
            SemanticWorldModelQuestionKind.WhyClassification =>
                TryWhy(request.Subject, request.Concept ?? string.Empty, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.Mechanism =>
                TryExplainMechanism(request.Subject, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.Causes =>
                TryExplainCauses(request.Subject, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.Compare =>
                TryCompare(request.Subject, request.OtherSubject ?? string.Empty, model, reasoner, out message, out evidence),
            _ => Fail(out message, out evidence)
        };
    }

    internal static bool TryDefinition(
        string concept,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        var individual = model.FindConcept(concept, SemanticConceptKind.Individual);
        if (individual is not null)
        {
            return TryDescribeIndividual(individual, model, reasoner, out message, out evidence);
        }

        var type = model.FindConcept(concept, SemanticConceptKind.Type);
        var definition = type is null ? null : model.FindDefinition(type.Id);
        if (type is null || definition is null || definition.ParentTypes.IsDefaultOrEmpty)
        {
            return false;
        }

        var parent = model.FindConcept(definition.ParentTypes[0], SemanticConceptKind.Type);
        if (parent is null)
        {
            return false;
        }

        var conditionPhrases = definition.EquivalentConditions
            .Select(condition => FormatCondition(condition, model))
            .Where(phrase => phrase is not null)
            .ToArray();
        if (conditionPhrases.Length == 0)
        {
            return false;
        }

        message = $"A {type.Label} is {Indefinite(parent.Label)} {parent.Label} that {JoinConditions(conditionPhrases)}.";
        evidence = ImmutableArray.Create(ToEvidence([definition.Source], LatticeEvidenceKind.Asserted));
        return true;
    }

    private static bool TryDescribeIndividual(
        SemanticConcept individual,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var types = reasoner.GetTypes(individual.Id);
        var specificTypes = types
            .Where(candidate => !types.Any(other =>
                other != candidate && IsSubtype(other, candidate, model)))
            .Select(type => model.FindConcept(type, SemanticConceptKind.Type))
            .Where(type => type is not null && type.Id is not (
                "entity" or "software" or "capability" or "research-publication"))
            .ToArray();

        if (specificTypes.Length == 0)
        {
            return false;
        }

        var clauses = new List<string>
        {
            $"{individual.Label} is {JoinConditions(specificTypes.Select(DescribeTypeMembership).ToArray())}."
        };
        var sources = specificTypes
            .SelectMany(type => reasoner.GetMembershipEvidence(individual.Id, type!.Id))
            .ToList();

        var capabilityAssertions = reasoner.GetRelations(individual.Id, "supports-capability");
        var capabilities = capabilityAssertions
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .Where(target => target is not null)
            .Select(target => target!)
            .DistinctBy(target => target.Id)
            .ToArray();

        if (capabilities.Length > 0)
        {
            clauses.Add($"It supports {JoinConditions(capabilities.Select(target => target.Label).ToArray())}.");
            sources.AddRange(capabilityAssertions.Select(assertion => assertion.Source));
            foreach (var capability in capabilities)
            {
                sources.AddRange(reasoner.GetMembershipEvidence(capability.Id, "capability"));
            }
        }

        var documentation = model.GetDataAssertions(individual.Id, "documentation-url");
        if (documentation.Length > 0)
        {
            clauses.Add($"Its documentation is available at {documentation[0].Value}.");
            sources.AddRange(documentation.Select(assertion => assertion.Source));
        }

        message = string.Join(" ", clauses);
        evidence = ImmutableArray.Create(ToEvidence(
            sources.Distinct().ToImmutableArray(),
            LatticeEvidenceKind.Derived));
        return true;
    }

    private static string DescribeTypeMembership(SemanticConcept type)
    {
        if (type.Id == "open-source-software")
        {
            return type.Label;
        }

        return Indefinite(type.Label);
    }

    private static bool TryExplainCauses(
        string requestedConcept,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var type = model.FindConcept(requestedConcept, SemanticConceptKind.Type);
        if (type is null)
        {
            return false;
        }

        var instances = model.Concepts
            .Where(concept =>
                concept.Kind == SemanticConceptKind.Individual &&
                reasoner.IsInstanceOf(concept.Id, type.Id))
            .ToArray();
        var mechanisms = instances
            .SelectMany(instance => reasoner.GetRelations(instance.Id, "has-mechanism"))
            .ToArray();
        if (mechanisms.Length == 0)
        {
            return false;
        }

        var labels = mechanisms
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .Where(concept => concept is not null)
            .Select(concept => concept!.Label)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (labels.Length == 0)
        {
            return false;
        }

        message = $"{Capitalize(type.Label)} can arise through {JoinConditions(labels)}.";
        var sources = mechanisms.Select(assertion => assertion.Source)
            .Concat(instances.SelectMany(instance =>
                reasoner.GetMembershipEvidence(instance.Id, type.Id)))
            .ToImmutableArray();
        evidence = ImmutableArray.Create(ToEvidence(sources.Distinct().ToImmutableArray(), LatticeEvidenceKind.Derived));
        return true;
    }

    internal static bool TryClassify(
        MultiItemClassificationFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var itemType = request.ItemType.Value;
        var results = new List<(string Item, ImmutableArray<string> Categories, ImmutableArray<SemanticWorldModelSource> Sources)>();

        foreach (var requestedItem in request.Items)
        {
            var item = model.FindConcept(requestedItem.Value, SemanticConceptKind.Individual);
            if (item is null)
            {
                return false;
            }

            var matching = new List<(string Label, ImmutableArray<SemanticWorldModelSource> Sources)>();
            foreach (var category in request.Categories)
            {
                var categoryType = model.FindConcept(
                    $"{category.Value}-{itemType}",
                    SemanticConceptKind.Type);
                if (categoryType is null)
                {
                    continue;
                }

                var sources = reasoner.GetMembershipEvidence(item.Id, categoryType.Id);
                if (!sources.IsDefaultOrEmpty)
                {
                    matching.Add((category.Value, sources));
                }
            }

            if (matching.Count == 0)
            {
                return false;
            }

            results.Add((
                item.Label,
                matching.Select(item => item.Label).ToImmutableArray(),
                matching.SelectMany(item => item.Sources).Distinct().ToImmutableArray()));
        }

        message = string.Join(
            " ",
            results.Select(result => result.Categories.Length switch
            {
                1 => $"{Capitalize(result.Item)} is {Indefinite(result.Categories[0])} {result.Categories[0]} {itemType}.",
                2 => $"{Capitalize(result.Item)} is both a {result.Categories[0]} and a {result.Categories[1]} {itemType}.",
                _ => $"{Capitalize(result.Item)} belongs to the {JoinNames(result.Categories)} {itemType} categories."
            }));

        evidence = results
            .Select(result => ToEvidence(result.Sources, LatticeEvidenceKind.Derived))
            .ToImmutableArray();
        return true;
    }

    internal static bool TryAskMembership(
        string individual,
        string type,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var item = model.FindConcept(individual, SemanticConceptKind.Individual);
        var targetType = model.FindConcept(type, SemanticConceptKind.Type);
        if (item is null || targetType is null)
        {
            return false;
        }

        var sources = reasoner.GetMembershipEvidence(item.Id, targetType.Id);
        if (sources.IsDefaultOrEmpty)
        {
            return false;
        }

        evidence = ImmutableArray.Create(ToEvidence(sources, LatticeEvidenceKind.Derived));
        return true;
    }

    private static bool TryWhy(
        string individualId,
        string typeId,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var individual = model.FindConcept(individualId, SemanticConceptKind.Individual);
        var type = model.FindConcept(typeId, SemanticConceptKind.Type);
        var definition = type is null ? null : model.FindDefinition(type.Id);
        if (individual is null || type is null || definition is null)
        {
            return false;
        }

        var matchedCondition = definition.EquivalentConditions.FirstOrDefault(condition =>
            reasoner.GetRelations(individual.Id, condition.Relation).Any(assertion =>
                reasoner.IsInstanceOf(assertion.Object, condition.TargetType)));
        if (matchedCondition is null)
        {
            return false;
        }

        var relationAssertion = reasoner.GetRelations(individual.Id, matchedCondition.Relation)
            .First(assertion => reasoner.IsInstanceOf(assertion.Object, matchedCondition.TargetType));
        var phrase = FormatCondition(matchedCondition, model);
        if (phrase is null)
        {
            return false;
        }

        message = $"{Capitalize(Indefinite(individual.Label))} {individual.Label} is {Indefinite(type.Label)} {type.Label} because it {phrase}.";
        var sources = ImmutableArray.Create(definition.Source, relationAssertion.Source)
            .AddRange(GetTypeEvidence(relationAssertion.Object, matchedCondition.TargetType, model));
        evidence = ImmutableArray.Create(ToEvidence(sources.Distinct().ToImmutableArray(), LatticeEvidenceKind.Derived));
        return true;
    }

    private static bool TryExplainMechanism(
        string individualId,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var individual = model.FindConcept(individualId, SemanticConceptKind.Individual);
        if (individual is null)
        {
            return false;
        }

        var mechanismAssertions = reasoner.GetRelations(individual.Id, "has-sound-production-mechanism");
        if (mechanismAssertions.IsDefaultOrEmpty)
        {
            return false;
        }

        var sentences = new List<string>();
        var sources = new List<SemanticWorldModelSource>();
        var visited = new HashSet<string>(StringComparer.Ordinal);

        foreach (var mechanism in mechanismAssertions)
        {
            var relation = model.FindRelation(mechanism.Relation);
            var target = model.FindConcept(mechanism.Object, SemanticConceptKind.Individual);
            if (relation is null || target is null)
            {
                continue;
            }

            sentences.Add(FormatRelation(individual, target, relation, first: true));
            sources.Add(mechanism.Source);
            AppendMechanismChain(target.Id, model, reasoner, visited, sentences, sources, 0);
        }

        if (sentences.Count == 0)
        {
            return false;
        }

        message = string.Join(" ", sentences);
        evidence = ImmutableArray.Create(ToEvidence(sources.Distinct().ToImmutableArray(), LatticeEvidenceKind.Derived));
        return true;
    }

    private static void AppendMechanismChain(
        string subjectId,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        HashSet<string> visited,
        List<string> sentences,
        List<SemanticWorldModelSource> sources,
        int depth)
    {
        if (depth >= 6 || !visited.Add(subjectId))
        {
            return;
        }

        foreach (var assertion in reasoner.GetRelations(subjectId))
        {
            var relation = model.FindRelation(assertion.Relation);
            var subject = model.FindConcept(assertion.Subject, SemanticConceptKind.Individual);
            var target = model.FindConcept(assertion.Object, SemanticConceptKind.Individual);
            if (relation is null || subject is null || target is null)
            {
                continue;
            }

            sentences.Add(FormatRelation(subject, target, relation, first: false));
            sources.Add(assertion.Source);
            AppendMechanismChain(target.Id, model, reasoner, visited, sentences, sources, depth + 1);
        }
    }

    private static bool TryCompare(
        string firstId,
        string secondId,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var first = model.FindConcept(firstId, SemanticConceptKind.Individual);
        var second = model.FindConcept(secondId, SemanticConceptKind.Individual);
        if (first is null || second is null)
        {
            return false;
        }

        var clauses = new List<string>();
        var sources = new List<SemanticWorldModelSource>();
        foreach (var relation in model.Relations
            .Where(relation => relation.ComparisonPriority > 0)
            .OrderBy(relation => relation.ComparisonPriority))
        {
            var firstFacts = reasoner.GetRelations(first.Id, relation.Id);
            var secondFacts = reasoner.GetRelations(second.Id, relation.Id);
            if (firstFacts.IsDefaultOrEmpty || secondFacts.IsDefaultOrEmpty)
            {
                continue;
            }

            var firstObject = model.FindConcept(firstFacts[0].Object, SemanticConceptKind.Individual);
            var secondObject = model.FindConcept(secondFacts[0].Object, SemanticConceptKind.Individual);
            if (firstObject is null || secondObject is null)
            {
                continue;
            }

            var firstType = GetMostSpecificType(firstObject.Id, model, reasoner);
            var secondType = GetMostSpecificType(secondObject.Id, model, reasoner);
            if (firstType is null || secondType is null)
            {
                continue;
            }

            if (firstType.Id == secondType.Id)
            {
                var verb = relation.Verb == "has" ? "have" : relation.Verb;
                var role = relation.ObjectRole?.Replace("its", "their", StringComparison.Ordinal) ?? string.Empty;
                clauses.Add($"Both {verb} {Indefinite(firstType.Label)} {firstType.Label} {role}".TrimEnd());
            }
            else
            {
                clauses.Add($"{Capitalize(first.Label)} {relation.Verb} {Indefinite(firstType.Label)} {firstType.Label}, while {second.Label} {relation.Verb} {Indefinite(secondType.Label)} {secondType.Label}");
            }

            sources.Add(firstFacts[0].Source);
            sources.Add(secondFacts[0].Source);
        }

        if (clauses.Count == 0)
        {
            return false;
        }

        message = string.Join(". ", clauses).TrimEnd('.') + ".";
        evidence = ImmutableArray.Create(ToEvidence(sources.Distinct().ToImmutableArray(), LatticeEvidenceKind.Derived));
        return true;
    }

    private static SemanticConcept? GetMostSpecificType(
        string individual,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner)
    {
        var types = reasoner.GetTypes(individual);
        return types
            .Where(type => !types.Any(other =>
                other != type && IsSubtype(other, type, model)))
            .Select(type => model.FindConcept(type, SemanticConceptKind.Type))
            .Where(type => type is not null)
            .OrderBy(type => type!.Label, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static bool IsSubtype(string candidate, string expected, SemanticWorldModel model)
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

    private static ImmutableArray<SemanticWorldModelSource> GetTypeEvidence(
        string individual,
        string type,
        SemanticWorldModel model) =>
        model.TypeAssertions
            .Where(assertion =>
                assertion.Individual == individual &&
                IsSubtype(assertion.Type, type, model))
            .Select(assertion => assertion.Source)
            .ToImmutableArray();

    private static string? FormatCondition(SemanticRestriction condition, SemanticWorldModel model)
    {
        var relation = model.FindRelation(condition.Relation);
        var target = model.FindConcept(condition.TargetType, SemanticConceptKind.Type);
        if (relation is null || target is null)
        {
            return null;
        }

        var phrase = $"{relation.Verb} {Indefinite(target.Label)} {target.Label}";
        return relation.ObjectRole is null
            ? phrase
            : $"{phrase} {relation.ObjectRole}";
    }

    private static string FormatRelation(
        SemanticConcept subject,
        SemanticConcept target,
        SemanticRelationDefinition relation,
        bool first)
    {
        var prefix = first ? $"{Capitalize(Indefinite(subject.Label))} {subject.Label}" : $"The {subject.Label}";
        var objectPhrase = relation.ObjectRole is null
            ? $"the {target.Label}"
            : $"{Indefinite(target.Label)} {target.Label} {relation.ObjectRole}";
        var verb = subject.Label.EndsWith('s') && subject.Label.Length > 1
            ? InflectPluralSubjectVerb(relation.Verb)
            : relation.Verb;
        return $"{prefix} {verb} {objectPhrase}.";
    }

    private static string FormatRelationTarget(SemanticRelationDefinition relation, string targetLabel) =>
        relation.ObjectRole is null
            ? $"{relation.Verb} {Indefinite(targetLabel)} {targetLabel}"
            : $"{relation.Verb} {Indefinite(targetLabel)} {targetLabel} {relation.ObjectRole}";

    private static string InflectPluralSubjectVerb(string verbPhrase)
    {
        var separator = verbPhrase.IndexOf(' ');
        var verb = separator < 0 ? verbPhrase : verbPhrase[..separator];
        var remainder = separator < 0 ? string.Empty : verbPhrase[separator..];
        var baseVerb = verb switch
        {
            "has" => "have",
            "does" => "do",
            _ when verb.EndsWith("ies", StringComparison.Ordinal) => $"{verb[..^3]}y",
            _ when verb.EndsWith("s", StringComparison.Ordinal) => verb[..^1],
            _ => verb
        };
        return $"{baseVerb}{remainder}";
    }

    private static string JoinConditions(IReadOnlyList<string> phrases) =>
        phrases.Count switch
        {
            1 => phrases[0],
            2 => $"{phrases[0]} and {phrases[1]}",
            _ => $"{string.Join(", ", phrases.Take(phrases.Count - 1))}, and {phrases[^1]}"
        };

    private static string JoinNames(IReadOnlyList<string> names) =>
        names.Count switch
        {
            0 => string.Empty,
            1 => names[0],
            2 => $"{names[0]} and {names[1]}",
            _ => $"{string.Join(", ", names.Take(names.Count - 1))}, and {names[^1]}"
        };

    private static string Indefinite(string phrase) =>
        phrase.Length > 0 && "aeiou".Contains(char.ToLowerInvariant(phrase[0]))
            ? "an"
            : "a";

    private static string Capitalize(string value) =>
        value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];

    private static LatticeEvidence ToEvidence(
        ImmutableArray<SemanticWorldModelSource> sources,
        LatticeEvidenceKind kind) =>
        new(
            kind,
            LatticeEvidencePolarity.Positive,
            sources.Select(source => new LatticeEvidenceSource(
                LatticeKnowledgeSourceKind.ImportedClaim,
                source.SourceText,
                source.SourceId,
                null,
                source.SourceUrl)).ToImmutableArray(),
            ImmutableArray<LatticeEvidence>.Empty);

    private static bool Fail(
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        return false;
    }
}
