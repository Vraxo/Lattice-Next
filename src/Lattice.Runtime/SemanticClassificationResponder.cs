using System.Collections.Immutable;
using System.Globalization;
using Lattice.Client;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

using static Lattice.Runtime.SemanticResponseFormatting;
namespace Lattice.Runtime;

internal static class SemanticClassificationResponder
{

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
            var item = model.FindConcept(requestedItem.Value);
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

                var sources = item.Kind switch
                {
                    SemanticConceptKind.Individual =>
                        reasoner.GetMembershipEvidence(item.Id, categoryType.Id),
                    SemanticConceptKind.Type =>
                        GetSubtypeEvidence(item.Id, categoryType.Id, model),
                    _ => ImmutableArray<SemanticWorldModelSource>.Empty
                };
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

        var groups = request.Categories
            .Select(category => (
                Category: category.Value,
                Items: results
                    .Where(result => result.Categories.Contains(category.Value))
                    .Select(result => result.Item)
                    .ToArray()))
            .Where(group => group.Items.Length > 0)
            .ToArray();

        message = string.Join(
            " ",
            groups.Select(group => FormatClassificationGroup(
                group.Category,
                group.Items,
                itemType)));

        evidence = results
            .Select(result => ToEvidence(result.Sources, LatticeEvidenceKind.Derived))
            .ToImmutableArray();
        return true;
    }

    private static string FormatClassificationGroup(
        string category,
        IReadOnlyList<string> items,
        string itemType)
    {
        var categoryPhrase = category.Replace('-', ' ');
        var itemNames = Capitalize(JoinNames(items
            .Select(item => item.Length == 0
                ? item
                : $"{char.ToLowerInvariant(item[0])}{item[1..]}")
            .ToArray()));

        if (itemType.StartsWith("thing-for-", StringComparison.Ordinal))
        {
            var purpose = itemType["thing-for-".Length..].Replace('-', ' ');
            return $"{Capitalize(categoryPhrase)} for {purpose}: {itemNames}.";
        }

        var typePhrase = itemType.Replace('-', ' ');
        var pluralType = typePhrase.EndsWith("s", StringComparison.Ordinal)
            ? typePhrase
            : $"{typePhrase}s";

        return $"{Capitalize(categoryPhrase)} {pluralType}: {itemNames}.";
    }

    private static ImmutableArray<SemanticWorldModelSource> GetSubtypeEvidence(
        string candidate,
        string expected,
        SemanticWorldModel model)
    {
        if (candidate == expected)
        {
            return [];
        }

        var pending = new Queue<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal) { candidate };
        var previous = new Dictionary<string, SemanticRelationAssertion>(StringComparer.Ordinal);
        pending.Enqueue(candidate);

        while (pending.TryDequeue(out var current))
        {
            foreach (var assertion in model.RelationAssertions.Where(assertion =>
                assertion.Subject == current &&
                assertion.Relation == "is-a"))
            {
                if (!visited.Add(assertion.Object))
                {
                    continue;
                }

                previous[assertion.Object] = assertion;
                if (assertion.Object == expected)
                {
                    var sources = new List<SemanticWorldModelSource>();
                    var type = expected;
                    while (type != candidate)
                    {
                        var step = previous[type];
                        sources.Add(step.Source);
                        type = step.Subject;
                    }

                    sources.Reverse();
                    return sources.ToImmutableArray();
                }

                pending.Enqueue(assertion.Object);
            }
        }

        return [];
    }

    internal static bool TryChoice(
        ChoiceQuestionFormula question,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(reasoner);

        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var category = model.FindConcept(question.Category.Value, SemanticConceptKind.Type);
        if (category is null)
        {
            return false;
        }

        var supported = question.Choices
            .Select(choice => (
                Choice: model.FindConcept(choice.Value, SemanticConceptKind.Individual),
                Evidence: model.FindConcept(choice.Value, SemanticConceptKind.Individual) is { } individual
                    ? reasoner.GetMembershipEvidence(individual.Id, category.Id)
                    : ImmutableArray<SemanticWorldModelSource>.Empty))
            .Where(candidate => candidate.Choice is not null && !candidate.Evidence.IsDefaultOrEmpty)
            .ToArray();

        if (supported.Length != 1)
        {
            return false;
        }

        message = $"{Capitalize(supported[0].Choice!.Label)} is a species of {category.Label}.";
        evidence = ImmutableArray.Create(ToEvidence(
            supported[0].Evidence,
            LatticeEvidenceKind.Derived));
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
}
