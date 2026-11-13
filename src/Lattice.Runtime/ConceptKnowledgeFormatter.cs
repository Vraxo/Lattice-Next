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
        var visitedIndividuals = new HashSet<SemanticTerm>();
        var visitedRuleConcepts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var evidence = ImmutableArray.CreateBuilder<LatticeEvidence>();
        var lines = new List<string>();

        AppendConcept(root, facts, 0, visitedIndividuals, lines, evidence);
        AppendGeneralRules(
            root.Identifier.Value,
            knowledgeBase.Rules,
            0,
            visitedRuleConcepts,
            lines,
            evidence);

        if (lines.Count == 0)
        {
            return false;
        }

        var message = new StringBuilder()
            .Append("The available knowledge represents ")
            .Append(DisplayIdentifier(root.Identifier.Value))
            .AppendLine(" through these connected facts and general rules:")
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
            evidence.Count >= MaximumFacts ||
            !visited.Add(concept))
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

    private static void AppendGeneralRules(
        string concept,
        IReadOnlyList<UniversalRule> rules,
        int depth,
        HashSet<string> visited,
        List<string> lines,
        ImmutableArray<LatticeEvidence>.Builder evidence)
    {
        if (depth > MaximumDepth ||
            visited.Count >= MaximumNodes ||
            evidence.Count >= MaximumFacts ||
            !visited.Add(concept))
        {
            return;
        }

        var applicableRules = rules
            .Where(rule =>
                rule.Premises.Length == 1 &&
                rule.NegativePremises.IsDefaultOrEmpty &&
                rule.RelationalPremises.IsDefaultOrEmpty &&
                rule.NegativeRelationalPremises.IsDefaultOrEmpty &&
                rule.Premises[0].Arguments.Length == 1 &&
                string.Equals(
                    rule.Premises[0].Predicate.Value,
                    concept,
                    StringComparison.OrdinalIgnoreCase))
            .OrderBy(rule => rule.SourceText, StringComparer.Ordinal)
            .ToArray();

        foreach (var rule in applicableRules)
        {
            if (evidence.Count >= MaximumFacts)
            {
                return;
            }

            var indent = new string(' ', depth * 2);
            var subject = DisplayIdentifier(concept);

            if (rule.ExistentialConclusion is { } existential)
            {
                var conditions = existential.Conditions
                    .Select(condition => DisplayIdentifier(condition.Predicate.Value))
                    .ToArray();

                if (conditions.Length == 0)
                {
                    continue;
                }

                var descriptor = string.Join(" ", conditions);
                lines.Add(
                    $"{indent}- every {subject} {DisplayIdentifier(existential.Relation.Predicate.Value)} → {descriptor}");
                evidence.Add(MapRuleEvidence(rule));

                AppendGeneralRules(
                    existential.Conditions[0].Predicate.Value,
                    rules,
                    depth + 1,
                    visited,
                    lines,
                    evidence);
                continue;
            }

            if (rule.Conclusion.Arguments.Length != 1)
            {
                continue;
            }

            var polarity = rule.ConclusionPolarity == EvidencePolarity.Negative
                ? "not "
                : string.Empty;
            lines.Add(
                $"{indent}- every {subject} is → {polarity}{DisplayIdentifier(rule.Conclusion.Predicate.Value)}");
            evidence.Add(MapRuleEvidence(rule));
        }
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
            MapSources(proof.Sources),
            premises.ToImmutable());
    }

    private static LatticeEvidence MapRuleEvidence(UniversalRule rule) =>
        new(
            LatticeEvidenceKind.Asserted,
            rule.ConclusionPolarity == EvidencePolarity.Positive
                ? LatticeEvidencePolarity.Positive
                : LatticeEvidencePolarity.Negative,
            MapSources(rule.Sources),
            ImmutableArray<LatticeEvidence>.Empty);

    private static ImmutableArray<LatticeEvidenceSource> MapSources(
        ImmutableArray<KnowledgeSource> sources) =>
        sources
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
            .ToImmutableArray();
}
