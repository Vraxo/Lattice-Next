using System.Collections.Immutable;
using System.Text;
using Lattice.Client;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

namespace Lattice.Runtime;

internal sealed record ConceptKnowledgeExplanation(
    string Message,
    ImmutableArray<LatticeEvidence> Evidence);

internal static class ConceptKnowledgeFormatter
{
    private const int MaximumDepth = 8;
    private const int MaximumNodes = 32;
    private const int MaximumFacts = 64;

    internal static bool TryCreate(
        KnowledgeBase knowledgeBase,
        string concept,
        out ConceptKnowledgeExplanation? explanation)
    {
        ArgumentNullException.ThrowIfNull(knowledgeBase);

        explanation = null;

        if (string.IsNullOrWhiteSpace(concept))
        {
            return false;
        }

        var root = concept.Trim().Replace(' ', '-').ToLowerInvariant();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var evidence = ImmutableArray.CreateBuilder<LatticeEvidence>();
        var lines = new List<string>();

        if (!AppendConcept(knowledgeBase, root, 0, visited, lines, evidence) ||
            lines.Count == 0)
        {
            return false;
        }

        var message = new StringBuilder()
            .Append("The available knowledge represents ")
            .Append(DisplayIdentifier(root))
            .AppendLine(" through these connected facts:")
            .Append(string.Join(Environment.NewLine, lines))
            .ToString();

        explanation = new ConceptKnowledgeExplanation(message, evidence.ToImmutable());
        return true;
    }

    private static bool AppendConcept(
        KnowledgeBase knowledgeBase,
        string concept,
        int depth,
        HashSet<string> visited,
        List<string> lines,
        ImmutableArray<LatticeEvidence>.Builder evidence)
    {
        if (depth > MaximumDepth ||
            visited.Count >= MaximumNodes ||
            !visited.Add(concept) ||
            evidence.Count >= MaximumFacts)
        {
            return false;
        }

        var facts = knowledgeBase.Facts
            .Where(fact => HasSubject(fact, concept))
            .Where(fact =>
                fact.Predicate.Arguments.Length != 1 ||
                !string.Equals(
                    fact.Predicate.Predicate.Value,
                    concept,
                    StringComparison.OrdinalIgnoreCase))
            .OrderBy(fact => fact.Predicate.Arguments.Length == 1 ? 0 : 1)
            .ThenBy(fact => fact.Predicate.Predicate.Value, StringComparer.Ordinal)
            .ThenBy(fact => fact.Polarity)
            .ToArray();

        if (facts.Length == 0)
        {
            return false;
        }

        var indent = new string(' ', depth * 2);

        foreach (var fact in facts.Where(fact => fact.Predicate.Arguments.Length == 1))
        {
            if (evidence.Count >= MaximumFacts)
            {
                break;
            }

            var polarity = fact.Polarity == EvidencePolarity.Negative ? "not " : string.Empty;
            lines.Add($"{indent}- {polarity}is → {DisplayIdentifier(fact.Predicate.Predicate.Value)}");
            evidence.Add(MapEvidence(fact));
        }

        foreach (var fact in facts.Where(fact => fact.Predicate.Arguments.Length == 2))
        {
            if (evidence.Count >= MaximumFacts ||
                fact.Predicate.Arguments[1] is not IndividualTerm target ||
                string.IsNullOrWhiteSpace(target.Identifier.Value))
            {
                continue;
            }

            var polarity = fact.Polarity == EvidencePolarity.Negative ? "not " : string.Empty;
            lines.Add($"{indent}- {polarity}{DisplayIdentifier(fact.Predicate.Predicate.Value)} → {DisplayIdentifier(target.Identifier.Value)}");
            evidence.Add(MapEvidence(fact));

            if (fact.Polarity == EvidencePolarity.Positive &&
                depth < MaximumDepth &&
                visited.Count < MaximumNodes)
            {
                AppendConcept(
                    knowledgeBase,
                    target.Identifier.Value,
                    depth + 1,
                    visited,
                    lines,
                    evidence);
            }
        }

        return facts.Length > 0;
    }

    private static bool HasSubject(KnowledgeFact fact, string concept) =>
        !fact.Predicate.Arguments.IsDefaultOrEmpty &&
        fact.Predicate.Arguments[0] is IndividualTerm subject &&
        string.Equals(subject.Identifier.Value, concept, StringComparison.OrdinalIgnoreCase);

    private static string DisplayIdentifier(string identifier) =>
        identifier.Replace('-', ' ');

    private static LatticeEvidence MapEvidence(KnowledgeFact fact) =>
        new(
            LatticeEvidenceKind.Asserted,
            fact.Polarity == EvidencePolarity.Positive
                ? LatticeEvidencePolarity.Positive
                : LatticeEvidencePolarity.Negative,
            fact.Sources
                .Select(source => new LatticeEvidenceSource(
                    source.Kind switch
                    {
                        KnowledgeSourceKind.UserAssertion => LatticeKnowledgeSourceKind.UserAssertion,
                        KnowledgeSourceKind.ImportedClaim => LatticeKnowledgeSourceKind.ImportedClaim,
                        KnowledgeSourceKind.ExternalObservation => LatticeKnowledgeSourceKind.ExternalObservation,
                        _ => throw new ArgumentOutOfRangeException(nameof(source), source.Kind, "Unsupported knowledge source kind.")
                    },
                    source.SourceText,
                    source.SourceId,
                    source.ResolvedReferent?.Value))
                .ToImmutableArray(),
            ImmutableArray<LatticeEvidence>.Empty);
}
