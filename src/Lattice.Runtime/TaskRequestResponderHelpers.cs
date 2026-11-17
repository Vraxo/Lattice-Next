using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Lattice.Client;
using Lattice.Core.Generation;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

namespace Lattice.Runtime;

internal static class TaskRequestResponderHelpers
{
    internal static string EnsureSentenceEnding(string value)
    {
        return value.TrimEnd().TrimEnd('.', '!', '?') + ".";
    }

    internal static bool IsSubtype(string candidate, string expected, SemanticWorldModel model)
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

    internal static LatticeEvidenceSource ToEvidenceSource(SemanticWorldModelSource source) =>
        new(
            LatticeKnowledgeSourceKind.FoundationalKnowledge,
            source.SourceId,
            source.SourceId,
            null,
            null);

    internal static string IndefinitePhrase(string value) =>
        value.Length > 0 && "aeiou".Contains(char.ToLowerInvariant(value[0]))
            ? $"an {value}"
            : $"a {value}";

    internal static string JoinNames(IReadOnlyList<string> names) =>
        names.Count switch
        {
            0 => string.Empty,
            1 => names[0],
            2 => $"{names[0]} and {names[1]}",
            _ => $"{string.Join(", ", names.Take(names.Count - 1))}, and {names[^1]}"
        };

    internal static bool Fail(
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        return false;
    }

    internal static bool TryDescribeRuralMarket(
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<SemanticWorldModelSource> sources)
    {
        message = string.Empty;
        sources = ImmutableArray<SemanticWorldModelSource>.Empty;
        var market = model.FindConcept("rural-town-labor-market", SemanticConceptKind.Individual);
        if (market is null)
        {
            return false;
        }

        var dominantBuyer = reasoner.GetRelations(market.Id, "has-dominant-buyer").FirstOrDefault();
        var workers = reasoner.GetRelations(market.Id, "has-worker-group").FirstOrDefault();
        if (dominantBuyer is null || workers is null)
        {
            return false;
        }

        var buyer = model.FindConcept(dominantBuyer.Object, SemanticConceptKind.Individual);
        var workerGroup = model.FindConcept(workers.Object, SemanticConceptKind.Individual);
        if (buyer is null || workerGroup is null ||
            !reasoner.IsInstanceOf(workerGroup.Id, "limited-outside-options"))
        {
            return false;
        }

        message = $"For example, a labour market in a rural town has {IndefinitePhrase(buyer.Label)} as its dominant buyer and includes {workerGroup.Label}, whose outside options are limited.";
        sources = ImmutableArray.Create(dominantBuyer.Source, workers.Source)
            .AddRange(reasoner.GetMembershipEvidence(workerGroup.Id, "limited-outside-options"));
        return true;
    }

    internal static bool TryDescribeRegionalMarket(
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<SemanticWorldModelSource> sources)
    {
        message = string.Empty;
        sources = ImmutableArray<SemanticWorldModelSource>.Empty;
        var market = model.FindConcept("regional-healthcare-labor-market", SemanticConceptKind.Individual);
        if (market is null)
        {
            return false;
        }

        var employers = reasoner.GetRelations(market.Id, "has-employer");
        var features = reasoner.GetRelations(market.Id, "has-market-feature");
        if (employers.Length < 2 || features.IsDefaultOrEmpty)
        {
            return false;
        }

        var featureLabels = features
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .Where(concept => concept is not null)
            .Select(concept => concept!.Label)
            .ToArray();
        if (featureLabels.Length == 0)
        {
            return false;
        }

        message = $"A regional healthcare labour market can have several employers while still exhibiting {JoinNames(featureLabels)}.";
        sources = employers.Select(assertion => assertion.Source)
            .Concat(features.Select(assertion => assertion.Source))
            .ToImmutableArray();
        return true;
    }
}
