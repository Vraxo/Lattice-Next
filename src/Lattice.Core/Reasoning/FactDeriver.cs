using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

internal static class FactDeriver
{
    internal static List<ProofNode> DeriveUnaryFacts(KnowledgeBase knowledgeBase) =>
        DeriveUnaryEvidence(knowledgeBase).Positive;

    internal static List<ProofNode> DeriveNegativeUnaryFacts(KnowledgeBase knowledgeBase) =>
        DeriveUnaryEvidence(knowledgeBase).Negative;

    private static (List<ProofNode> Positive, List<ProofNode> Negative) DeriveUnaryEvidence(
        KnowledgeBase knowledgeBase)
    {
        var positiveFacts = new List<ProofNode>();
        var negativeFacts = new List<ProofNode>();
        var positiveProofsByFact =
            new Dictionary<(string Predicate, SemanticTerm Individual), List<ProofNode>>();
        var negativeProofsByFact =
            new Dictionary<(string Predicate, SemanticTerm Individual), List<ProofNode>>();

        foreach (var fact in knowledgeBase.Facts)
        {
            if (!ProofOperations.TryGetFactKey(fact.Predicate, out _))
            {
                continue;
            }

            var proof = ProofOperations.CreateAssertedProof(fact);

            if (fact.Polarity == EvidencePolarity.Positive)
            {
                ProofOperations.TryAddProof(positiveFacts, positiveProofsByFact, proof);
            }
            else
            {
                ProofOperations.TryAddProof(negativeFacts, negativeProofsByFact, proof);
            }
        }

        var positiveRelationalFacts = knowledgeBase.Facts
            .Where(fact => fact.Polarity == EvidencePolarity.Positive && !fact.Predicate.Arguments.IsDefault && fact.Predicate.Arguments.Length == 2)
            .Select(ProofOperations.CreateAssertedProof).ToArray();
        var negativeRelationalFacts = knowledgeBase.Facts
            .Where(fact => fact.Polarity == EvidencePolarity.Negative && !fact.Predicate.Arguments.IsDefault && fact.Predicate.Arguments.Length == 2)
            .Select(ProofOperations.CreateAssertedProof).ToArray();

        bool added;

        do
        {
            added = false;
            var candidateIndividuals = GetCandidateIndividuals(positiveFacts, negativeFacts);

            foreach (var rule in knowledgeBase.Rules)
            {
                if (rule.Premises.IsDefaultOrEmpty && rule.NegativePremises.IsDefaultOrEmpty && rule.RelationalPremises.IsDefaultOrEmpty && rule.NegativeRelationalPremises.IsDefaultOrEmpty ||
                    rule.Conclusion.Arguments.IsDefault ||
                    rule.Conclusion.Arguments.Length != 1)
                {
                    continue;
                }

                foreach (var individual in candidateIndividuals)
                {
                    var combinations = RulePremiseMatcher.GetRulePremiseCombinations(
                        rule,
                        individual,
                        positiveFacts,
                        negativeFacts,
                        positiveRelationalFacts,
                        negativeRelationalFacts);

                    foreach (var premises in combinations)
                    {
                        var conclusion = ProofOperations.CreateGroundPredicate(rule.Conclusion.Predicate, individual);

                        if (!ProofOperations.TryGetFactKey(conclusion, out var conclusionKey) ||
                            premises.Any(premise => ProofOperations.ContainsFact(
                                premise,
                                conclusionKey,
                                rule.ConclusionPolarity)))
                        {
                            continue;
                        }

                        var proof = new ProofNode(
                            conclusion,
                            rule.SourceText,
                            premises[0],
                            AdditionalPremises: premises.Skip(1).ToImmutableArray(),
                            SourceKind: rule.SourceKind,
                            AdditionalSources: rule.AdditionalSources,
                            Kind: ProofNodeKind.Derived,
                            SourceId: rule.SourceId)
                        {
                            Polarity = rule.ConclusionPolarity
                        };

                        var proofAdded = rule.ConclusionPolarity == EvidencePolarity.Positive
                            ? ProofOperations.TryAddProof(positiveFacts, positiveProofsByFact, proof)
                            : ProofOperations.TryAddProof(negativeFacts, negativeProofsByFact, proof);

                        if (proofAdded)
                        {
                            added = true;
                        }
                    }
                }
            }
        }
        while (added);

        return (positiveFacts, negativeFacts);
    }

    internal static List<ProofNode> DeriveRelationalFacts(
        KnowledgeBase knowledgeBase,
        EvidencePolarity polarity)
    {
        var (positiveFacts, negativeFacts) = DeriveUnaryEvidence(knowledgeBase);
        var positiveRelationalFacts = knowledgeBase.Facts
            .Where(fact => fact.Polarity == EvidencePolarity.Positive && !fact.Predicate.Arguments.IsDefault && fact.Predicate.Arguments.Length == 2)
            .Select(ProofOperations.CreateAssertedProof).ToArray();
        var negativeRelationalFacts = knowledgeBase.Facts
            .Where(fact => fact.Polarity == EvidencePolarity.Negative && !fact.Predicate.Arguments.IsDefault && fact.Predicate.Arguments.Length == 2)
            .Select(ProofOperations.CreateAssertedProof).ToArray();
        var relationalProofs = new List<ProofNode>();

        foreach (var rule in knowledgeBase.Rules)
        {
            if (rule.ConclusionPolarity != polarity ||
                rule.Premises.IsDefaultOrEmpty && rule.NegativePremises.IsDefaultOrEmpty && rule.RelationalPremises.IsDefaultOrEmpty && rule.NegativeRelationalPremises.IsDefaultOrEmpty ||
                rule.Conclusion.Arguments.IsDefault ||
                rule.Conclusion.Arguments.Length != 2 ||
                rule.Conclusion.Arguments[0] is not VariableTerm ||
                rule.Conclusion.Arguments[1] is not IndividualTerm objectTerm ||
                string.IsNullOrWhiteSpace(objectTerm.Identifier.Value))
            {
                continue;
            }

            var candidateIndividuals = GetCandidateIndividuals(positiveFacts, negativeFacts);

            foreach (var individual in candidateIndividuals)
            {
                var combinations = RulePremiseMatcher.GetRulePremiseCombinations(
                    rule,
                    individual,
                    positiveFacts,
                    negativeFacts,
                    positiveRelationalFacts,
                    negativeRelationalFacts);

                foreach (var premises in combinations)
                {
                    var conclusion = new PredicateApplication(
                        rule.Conclusion.Predicate,
                        ImmutableArray.Create<SemanticTerm>(individual, objectTerm));

                    ProofOperations.AddUniqueProof(
                        relationalProofs,
                        new ProofNode(
                            conclusion,
                            rule.SourceText,
                            premises[0],
                            AdditionalPremises: premises.Skip(1).ToImmutableArray(),
                            SourceKind: rule.SourceKind,
                            AdditionalSources: rule.AdditionalSources,
                            Kind: ProofNodeKind.Derived,
                            SourceId: rule.SourceId)
                        {
                            Polarity = polarity
                        });
                }
            }
        }

        return relationalProofs;
    }

    private static SemanticTerm[] GetCandidateIndividuals(
        IReadOnlyList<ProofNode> positiveFacts,
        IReadOnlyList<ProofNode> negativeFacts) =>
        positiveFacts
            .Concat(negativeFacts)
            .Select(proof => ProofOperations.TryGetFactKey(proof.Predicate, out var key) ? key.Individual : null)
            .OfType<SemanticTerm>()
            .Distinct()
            .ToArray();


}
