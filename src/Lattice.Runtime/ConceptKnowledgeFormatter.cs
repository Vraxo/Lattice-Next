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
        ReasoningEngine reasoningEngine,
        string concept,
        out ConceptKnowledgeExplanation? explanation)
    {
        ArgumentNullException.ThrowIfNull(knowledgeBase);
        ArgumentNullException.ThrowIfNull(reasoningEngine);

        explanation = null;

        if (string.IsNullOrWhiteSpace(concept))
        {
            return false;
        }

        var root = new IndividualTerm(new SemanticIdentifier(
            concept.Trim().Replace(' ', '-').ToLowerInvariant()));
        var facts = reasoningEngine.GetKnownFacts(knowledgeBase);
        var visited = new HashSet<SemanticTerm>();
        var evidence = ImmutableArray.CreateBuilder<LatticeEvidence>();
        var lines = new List<string>();

        if (!AppendConcept(root, facts, 0, visited, lines, evidence) ||
            lines.Count == 0)
        {
            return false;
        }

        var message = new StringBuilder()
            .Append("The available knowledge represents ")
            .Append(DisplayIdentifier(((IndividualTerm)root).Identifier.Value))
            .AppendLine(" through these connected facts:")
            .Append(string.Join(Environment.NewLine, lines))
            .ToString();

        explanation = new ConceptKnowledgeExplanation(message, evidence.ToImmutable());
        return true;
    }

    private static bool AppendConcept(
        SemanticTerm concept,
        ImmutableArray<ProofNode> facts,
        int depth,
        HashSet<SemanticTerm> visited,
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

        var factsForConcept = facts
            .Where(fact => HasSubject(fact.Predicate, concept))
            .Where(fact =>
                fact.Predicate.Arguments.Length != 1 ||
                !IsConceptIdentity(fact.Predicate, concept))
            .OrderBy(fact => fact.Predicate.Arguments.Length == 1 ? 0 : 1)
            .ThenBy(fact => fact.Predicate.Predicate.Value, StringComparer.Ordinal)
            .ThenBy(fact => fact.Polarity)
            .ToArray();

        if (factsForConcept.Length == 0)
        {
            return false;
        }

        var indent = new string(' ', depth * 2);

        foreach (var fact in factsForConcept.Where(fact => fact.Predicate.Arguments.Length == 1))
        {
            if (evidence.Count >= MaximumFacts)
            {
                break;
            }

            var polarity = fact.Polarity == EvidencePolarity.Negative ? "not " : string.Empty;
            lines.Add($"{indent}- {polarity}is → {DisplayIdentifier(fact.Predicate.Predicate.Value)}");
            evidence.Add(MapEvidence(fact));
        }

        foreach (var fact in factsForConcept.Where(fact => fact.Predicate.Arguments.Length == 2))
        {
            if (evidence.Count >= MaximumFacts ||
                fact.Predicate.Arguments[1] is not (IndividualTerm or AnonymousIndividualTerm))
            {
                continue;
            }

            var target = fact.Predicate.Arguments[1];
            var polarity = fact.Polarity == EvidencePolarity.Negative ? "not " : string.Empty;
            lines.Add($"{indent}- {polarity}{DisplayIdentifier(fact.Predicate.Predicate.Value)} → {DescribeIndividual(target, facts)}");
            evidence.Add(MapEvidence(fact));

            if (fact.Polarity == EvidencePolarity.Positive &&
                depth < MaximumDepth &&
                visited.Count < MaximumNodes)
            {
                AppendConcept(target, facts, depth + 1, visited, lines, evidence);
            }
        }

        return factsForConcept.Length > 0;
    }

    private static string DescribeIndividual(
        SemanticTerm individual,
        ImmutableArray<ProofNode> facts)
    {
        if (individual is IndividualTerm named)
        {
            return DisplayIdentifier(named.Identifier.Value);
        }

        var descriptor = facts
            .Where(fact =>
                fact.Polarity == EvidencePolarity.Positive &&
                fact.Predicate.Arguments.Length == 1 &&
                fact.Predicate.Arguments[0] == individual)
            .Select(fact => fact.Predicate.Predicate.Value)
            .Order(StringComparer.Ordinal)
            .FirstOrDefault();

        return descriptor is null
            ? "an instance"
            : $"a {DisplayIdentifier(descriptor)}";
    }

    private static bool HasSubject(
        PredicateApplication predicate,
        SemanticTerm concept) =>
        !predicate.Arguments.IsDefaultOrEmpty &&
        predicate.Arguments[0] == concept;

    private static bool IsConceptIdentity(
        PredicateApplication predicate,
        SemanticTerm concept) =>
        predicate.Arguments.Length == 1 &&
        predicate.Arguments[0] == concept &&
        concept is IndividualTerm individual &&
        string.Equals(
            predicate.Predicate.Value,
            individual.Identifier.Value,
            StringComparison.OrdinalIgnoreCase);

    private static string DisplayIdentifier(string identifier) =>
        identifier.Replace('-', ' ');

    private static LatticeEvidence MapEvidence(ProofNode proof)
    {
        var premises = ImmutableArray.CreateBuilder<LatticeEvidence>();

        if (proof.Premise is not null)
        {
            premises.Add(MapEvidence(proof.Premise));
        }

        if (!proof.AdditionalPremises.IsDefaultOrEmpty)
        {
            foreach (var premise in proof.AdditionalPremises)
            {
                premises.Add(MapEvidence(premise));
            }
        }

        return new LatticeEvidence(
            proof.Kind == ProofNodeKind.Asserted
                ? LatticeEvidenceKind.Asserted
                : LatticeEvidenceKind.Derived,
            proof.Polarity == EvidencePolarity.Positive
                ? LatticeEvidencePolarity.Positive
                : LatticeEvidencePolarity.Negative,
            proof.Sources
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
            premises.ToImmutable());
    }
}
