using System.Collections.Immutable;
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
        var evidence = ImmutableArray.CreateBuilder<LatticeEvidence>();
        var sentences = new List<string>();
        var visitedConcepts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AppendConcept(
            root,
            facts,
            knowledgeBase.Rules,
            0,
            visitedConcepts,
            sentences,
            evidence);

        if (sentences.Count == 0)
        {
            return false;
        }

        explanation = new ConceptKnowledgeExplanation(
            string.Join(" ", sentences),
            evidence
                .DistinctBy(GetEvidenceKey)
                .ToImmutableArray());
        return true;
    }

    private static void AppendConcept(
        SemanticTerm subject,
        ImmutableArray<ProofNode> facts,
        IReadOnlyList<UniversalRule> rules,
        int depth,
        HashSet<string> visited,
        List<string> sentences,
        ImmutableArray<LatticeEvidence>.Builder evidence)
    {
        if (depth > MaximumDepth ||
            evidence.Count >= MaximumFacts ||
            !visited.Add(GetConceptKey(subject)))
        {
            return;
        }

        var subjectName = GetSubjectName(subject, facts);
        var unaryFacts = facts
            .Where(fact =>
                fact.Polarity == EvidencePolarity.Positive &&
                fact.Predicate.Arguments.Length == 1 &&
                fact.Predicate.Arguments[0] == subject &&
                !IsConceptIdentity(fact.Predicate, subject))
            .OrderBy(fact => fact.Predicate.Predicate.Value, StringComparer.Ordinal)
            .DistinctBy(GetFactKey)
            .ToArray();

        var directRelations = facts
            .Where(fact =>
                fact.Polarity == EvidencePolarity.Positive &&
                fact.Predicate.Arguments.Length == 2 &&
                fact.Predicate.Arguments[0] == subject &&
                fact.Predicate.Arguments[1] is IndividualTerm)
            .OrderBy(fact => fact.Predicate.Predicate.Value, StringComparer.Ordinal)
            .DistinctBy(GetFactKey)
            .ToArray();

        var applicableRules = GetApplicableRules(subjectName, rules);
        var existentialRules = applicableRules
            .Where(rule => rule.ExistentialConclusion is not null)
            .ToArray();
        var unaryConclusionRules = applicableRules
            .Where(rule =>
                rule.ExistentialConclusion is null &&
                rule.Conclusion.Arguments.Length == 1)
            .ToArray();

        var descriptions = unaryFacts
            .Select(fact => DisplayIdentifier(fact.Predicate.Predicate.Value))
            .ToArray();
        var rootPrefix = Indefinite(subjectName);

        if (descriptions.Length > 0 || existentialRules.Length > 0)
        {
            var sentence = new System.Text.StringBuilder(rootPrefix);

            if (descriptions.Length > 0)
            {
                sentence.Append(" is ")
                    .Append(string.Join(" and ", descriptions.Select(Indefinite)));
            }

            foreach (var rule in existentialRules)
            {
                var existential = rule.ExistentialConclusion!;
                var descriptor = DescribeConditions(existential.Conditions);
                if (descriptor is null)
                {
                    continue;
                }

                var relation = DisplayIdentifier(existential.Relation.Predicate.Value);
                if (descriptions.Length > 0 &&
                    string.Equals(relation, "has", StringComparison.OrdinalIgnoreCase))
                {
                    sentence.Append(" with ");
                }
                else if (descriptions.Length > 0)
                {
                    sentence.Append(" that ")
                        .Append(relation)
                        .Append(' ');
                }
                else
                {
                    sentence.Append(' ')
                        .Append(relation)
                        .Append(' ');
                }

                sentence.Append(Indefinite(descriptor));
                evidence.Add(MapRuleEvidence(rule));
            }

            sentences.Add(Capitalize(sentence.Append('.').ToString()));

            foreach (var fact in unaryFacts)
            {
                evidence.Add(MapEvidence(fact));
            }
        }

        foreach (var relation in directRelations)
        {
            if (evidence.Count >= MaximumFacts ||
                relation.Predicate.Arguments[1] is not IndividualTerm target)
            {
                break;
            }

            var relationName = DisplayIdentifier(relation.Predicate.Predicate.Value);
            var targetName = DisplayIdentifier(target.Identifier.Value);
            sentences.Add($"{Capitalize(Indefinite(subjectName))} {relationName} {Indefinite(targetName)}.");
            evidence.Add(MapEvidence(relation));
            AppendConcept(
                target,
                facts,
                rules,
                depth + 1,
                visited,
                sentences,
                evidence);
        }

        foreach (var rule in existentialRules)
        {
            if (evidence.Count >= MaximumFacts)
            {
                break;
            }

            var existential = rule.ExistentialConclusion!;
            var descriptor = DescribeConditions(existential.Conditions);
            if (descriptor is null)
            {
                continue;
            }

            var relatedConcept = existential.Conditions[^1].Predicate.Value;
            AppendRelatedRules(
                relatedConcept,
                descriptor,
                rules,
                depth + 1,
                visited,
                sentences,
                evidence);
        }

        foreach (var rule in unaryConclusionRules)
        {
            if (evidence.Count >= MaximumFacts)
            {
                break;
            }

            var conclusion = DisplayIdentifier(rule.Conclusion.Predicate.Value);
            var polarity = rule.ConclusionPolarity == EvidencePolarity.Negative
                ? " is not "
                : " is ";
            sentences.Add($"Every {subjectName}{polarity}{conclusion}.");
            evidence.Add(MapRuleEvidence(rule));
        }
    }

    private static void AppendRelatedRules(
        string concept,
        string descriptor,
        IReadOnlyList<UniversalRule> rules,
        int depth,
        HashSet<string> visited,
        List<string> sentences,
        ImmutableArray<LatticeEvidence>.Builder evidence)
    {
        if (depth > MaximumDepth ||
            evidence.Count >= MaximumFacts ||
            !visited.Add(concept))
        {
            return;
        }

        var applicableRules = GetApplicableRules(concept, rules);
        foreach (var rule in applicableRules)
        {
            if (evidence.Count >= MaximumFacts)
            {
                return;
            }

            if (rule.ExistentialConclusion is { } existential)
            {
                var relatedDescriptor = DescribeConditions(existential.Conditions);
                if (relatedDescriptor is null)
                {
                    continue;
                }

                var relation = DisplayIdentifier(existential.Relation.Predicate.Value);
                sentences.Add($"Every {DisplayIdentifier(concept)} {relation} {Indefinite(relatedDescriptor)}.");
                evidence.Add(MapRuleEvidence(rule));
                AppendRelatedRules(
                    existential.Conditions[^1].Predicate.Value,
                    relatedDescriptor,
                    rules,
                    depth + 1,
                    visited,
                    sentences,
                    evidence);
                continue;
            }

            if (rule.Conclusion.Arguments.Length != 1)
            {
                continue;
            }

            var polarity = rule.ConclusionPolarity == EvidencePolarity.Negative
                ? " is not "
                : " is ";
            sentences.Add(
                $"Every {DisplayIdentifier(concept)}{polarity}{DisplayIdentifier(rule.Conclusion.Predicate.Value)}.");
            evidence.Add(MapRuleEvidence(rule));
        }
    }

    private static UniversalRule[] GetApplicableRules(
        string concept,
        IReadOnlyList<UniversalRule> rules) =>
        rules
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

    private static string? DescribeConditions(
        ImmutableArray<PredicateApplication> conditions)
    {
        if (conditions.IsDefaultOrEmpty)
        {
            return null;
        }

        var labels = conditions
            .Select(condition => DisplayIdentifier(condition.Predicate.Value))
            .ToArray();
        var head = labels[^1];
        var modifiers = labels.Take(labels.Length - 1);
        return string.Join(" ", modifiers.Append(head));
    }

    private static string GetSubjectName(
        SemanticTerm subject,
        ImmutableArray<ProofNode> facts) =>
        subject switch
        {
            IndividualTerm individual => DisplayIdentifier(individual.Identifier.Value),
            AnonymousIndividualTerm => DescribeIndividual(subject, facts),
            _ => "thing"
        };

    private static string DescribeIndividual(
        SemanticTerm individual,
        ImmutableArray<ProofNode> facts)
    {
        var descriptor = facts
            .Where(fact =>
                fact.Polarity == EvidencePolarity.Positive &&
                fact.Predicate.Arguments.Length == 1 &&
                fact.Predicate.Arguments[0] == individual)
            .Select(fact => fact.Predicate.Predicate.Value)
            .Order(StringComparer.Ordinal)
            .FirstOrDefault();

        return descriptor is null
            ? "instance"
            : DisplayIdentifier(descriptor);
    }

    private static string GetEvidenceKey(LatticeEvidence evidence) =>
        $"{evidence.Kind}:{evidence.Polarity}:{string.Join(";", evidence.Sources.Select(source => $"{source.Kind}:{source.SourceId}:{source.SourceText}"))}";

    private static string GetFactKey(ProofNode proof) =>
        $"{proof.Predicate.Predicate.Value}:{string.Join("|", proof.Predicate.Arguments)}:{proof.Polarity}";

    private static string GetConceptKey(SemanticTerm subject) =>
        subject switch
        {
            IndividualTerm individual => individual.Identifier.Value,
            AnonymousIndividualTerm anonymous => anonymous.Identity.Value,
            _ => subject.ToString() ?? string.Empty
        };

    private static bool IsConceptIdentity(
        PredicateApplication predicate,
        SemanticTerm subject) =>
        predicate.Arguments.Length == 1 &&
        predicate.Arguments[0] == subject &&
        subject is IndividualTerm individual &&
        string.Equals(
            predicate.Predicate.Value,
            individual.Identifier.Value,
            StringComparison.OrdinalIgnoreCase);

    private static string Indefinite(string phrase)
    {
        var trimmed = phrase.Trim();
        if (trimmed.Length == 0)
        {
            return trimmed;
        }

        var article = "aeiou".Contains(char.ToLowerInvariant(trimmed[0])) ? "an" : "a";
        return $"{article} {trimmed}";
    }

    private static string Capitalize(string value) =>
        value.Length == 0
            ? value
            : char.ToUpperInvariant(value[0]) + value[1..];

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
