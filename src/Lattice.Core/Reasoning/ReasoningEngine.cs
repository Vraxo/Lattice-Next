using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

public sealed class ReasoningEngine
{
    public ImmutableArray<ProofNode> GetKnownFacts(KnowledgeBase knowledgeBase)
    {
        ArgumentNullException.ThrowIfNull(knowledgeBase);

        var proofs = new List<ProofNode>();

        foreach (var proof in FactDeriver.DeriveUnaryFacts(knowledgeBase)
            .Concat(FactDeriver.DeriveNegativeUnaryFacts(knowledgeBase))
            .Concat(knowledgeBase.Facts
                .Where(fact => fact.Predicate.Arguments.Length == 2)
                .Select(ProofOperations.CreateAssertedProof))
            .Concat(FactDeriver.DeriveRelationalFacts(knowledgeBase, EvidencePolarity.Positive))
            .Concat(FactDeriver.DeriveRelationalFacts(knowledgeBase, EvidencePolarity.Negative))
            .Concat(FactDeriver.DeriveExistentialFacts(knowledgeBase)))
        {
            ProofOperations.AddUniqueProof(proofs, proof);
        }

        return proofs.ToImmutableArray();
    }

    public ReasoningResult Ask(
        KnowledgeBase knowledgeBase,
        PredicateApplication query)
    {
        ArgumentNullException.ThrowIfNull(knowledgeBase);
        ArgumentNullException.ThrowIfNull(query);

        if (!TryGroundQuery(query, out var groundedQuery))
        {
            return new ReasoningResult(null);
        }

        query = groundedQuery;

        IEnumerable<ProofNode> positiveProofs;

        if (query.Arguments.Length == 2)
        {
            positiveProofs = knowledgeBase.Facts
                .Where(fact =>
                    fact.Polarity == EvidencePolarity.Positive &&
                    ProofOperations.IsSameGroundPredicate(fact.Predicate, query))
                .Select(ProofOperations.CreateAssertedProof)
                .Concat(FactDeriver.DeriveRelationalFacts(knowledgeBase, EvidencePolarity.Positive).Where(candidate =>
                    ProofOperations.IsSameGroundPredicate(candidate.Predicate, query)))
                .Concat(FactDeriver.DeriveExistentialFacts(knowledgeBase).Where(candidate =>
                    ProofOperations.IsSameGroundPredicate(candidate.Predicate, query)));
        }
        else if (query.Arguments.Length == 1 &&
            ProofOperations.TryGetFactKey(query, out var queryKey))
        {
            positiveProofs = FactDeriver.DeriveUnaryFacts(knowledgeBase)
                .Concat(FactDeriver.DeriveExistentialFacts(knowledgeBase))
                .Where(candidate =>
                    ProofOperations.TryGetFactKey(candidate.Predicate, out var key) &&
                    key == queryKey);
        }
        else
        {
            positiveProofs = knowledgeBase.Facts
                .Where(fact =>
                    fact.Polarity == EvidencePolarity.Positive &&
                    ProofOperations.IsSameGroundPredicate(fact.Predicate, query))
                .Select(ProofOperations.CreateAssertedProof);
        }

        var negativeDerivedProofs = query.Arguments.Length switch
        {
            1 => FactDeriver.DeriveNegativeUnaryFacts(knowledgeBase),
            2 => FactDeriver.DeriveRelationalFacts(knowledgeBase, EvidencePolarity.Negative),
            _ => []
        };
        var negativeProofs = knowledgeBase.Facts
            .Where(fact =>
                fact.Polarity == EvidencePolarity.Negative &&
                ProofOperations.IsSameGroundPredicate(fact.Predicate, query))
            .Select(ProofOperations.CreateAssertedProof)
            .Concat(negativeDerivedProofs.Where(candidate =>
                ProofOperations.IsSameGroundPredicate(candidate.Predicate, query)));

        return ProofOperations.CreateResult(positiveProofs, negativeProofs);
    }

    public ReasoningResult AskRelated(
        KnowledgeBase knowledgeBase,
        SemanticIdentifier relation,
        SemanticTerm subject)
    {
        ArgumentNullException.ThrowIfNull(knowledgeBase);

        if (string.IsNullOrWhiteSpace(relation.Value) ||
            subject is not (IndividualTerm or AnonymousIndividualTerm))
        {
            return new ReasoningResult(null);
        }

        var positiveProofs = knowledgeBase.Facts
            .Where(fact =>
                fact.Polarity == EvidencePolarity.Positive &&
                IsRelationForSubject(fact.Predicate, relation, subject))
            .Select(ProofOperations.CreateAssertedProof)
            .Concat(FactDeriver.DeriveRelationalFacts(
                knowledgeBase,
                EvidencePolarity.Positive)
                .Where(proof => IsRelationForSubject(proof.Predicate, relation, subject)))
            .Concat(FactDeriver.DeriveExistentialFacts(knowledgeBase)
                .Where(proof => IsRelationForSubject(proof.Predicate, relation, subject)));

        var negativeProofs = knowledgeBase.Facts
            .Where(fact =>
                fact.Polarity == EvidencePolarity.Negative &&
                IsRelationForSubject(fact.Predicate, relation, subject))
            .Select(ProofOperations.CreateAssertedProof)
            .Concat(FactDeriver.DeriveRelationalFacts(
                knowledgeBase,
                EvidencePolarity.Negative)
                .Where(proof => IsRelationForSubject(proof.Predicate, relation, subject)));

        return ProofOperations.CreateResult(positiveProofs, negativeProofs);
    }

    private static bool IsRelationForSubject(
        PredicateApplication predicate,
        SemanticIdentifier relation,
        SemanticTerm subject) =>
        predicate.Predicate == relation &&
        !predicate.Arguments.IsDefault &&
        predicate.Arguments.Length == 2 &&
        predicate.Arguments[0] == subject;

    public ReasoningResult Ask(
        KnowledgeBase knowledgeBase,
        UniversalQuantification query)
    {
        ArgumentNullException.ThrowIfNull(knowledgeBase);
        ArgumentNullException.ThrowIfNull(query);

        if (!UniversalRuleFactory.TryCreate(
                query,
                string.Empty,
                KnowledgeSourceKind.UserAssertion,
                null,
                out var requestedRule,
                out _) ||
            requestedRule.Premises.IsDefaultOrEmpty ||
            !requestedRule.NegativePremises.IsDefaultOrEmpty ||
            !requestedRule.RelationalPremises.IsDefaultOrEmpty ||
            !requestedRule.NegativeRelationalPremises.IsDefaultOrEmpty ||
            requestedRule.Conclusion.Arguments.IsDefault ||
            requestedRule.Conclusion.Arguments.Length != 1)
        {
            return new ReasoningResult(null);
        }

        var matchingRules = knowledgeBase.Rules
            .Where(rule => IsApplicableUniversalRule(rule, requestedRule))
            .ToArray();
        var supportingProofs = matchingRules
            .Where(rule => rule.ConclusionPolarity == EvidencePolarity.Positive)
            .Select(CreateUniversalRuleProof)
            .ToList();
        var contradictingProofs = matchingRules
            .Where(rule => rule.ConclusionPolarity == EvidencePolarity.Negative)
            .Select(CreateUniversalRuleProof)
            .ToList();

        if (contradictingProofs.Count == 0)
        {
            contradictingProofs.AddRange(
                FindUniversalCounterexamples(knowledgeBase, requestedRule));
        }

        return ProofOperations.CreateResult(supportingProofs, contradictingProofs);
    }

    public ReasoningResult Ask(
        KnowledgeBase knowledgeBase,
        Negation query)
    {
        ArgumentNullException.ThrowIfNull(knowledgeBase);
        ArgumentNullException.ThrowIfNull(query);

        if (query.Formula is not PredicateApplication predicate)
        {
            return new ReasoningResult(null);
        }

        var result = Ask(knowledgeBase, predicate);

        return ProofOperations.CreateResult(
            result.ContradictingProofs,
            result.Proofs);
    }

    public ReasoningResult Ask(
        KnowledgeBase knowledgeBase,
        ExistentialQuantification query)
    {
        ArgumentNullException.ThrowIfNull(knowledgeBase);
        ArgumentNullException.ThrowIfNull(query);

        if (!TryGetBoundUnaryPredicates(query.Body, query.Variable, out var conditions))
        {
            return new ReasoningResult(null);
        }

        var knownFacts = FactDeriver.DeriveUnaryFacts(knowledgeBase);
        var proofs = new List<ProofNode>();

        foreach (var candidate in knownFacts)
        {
            if (!ProofOperations.TryGetFactKey(candidate.Predicate, out var candidateKey) ||
                !string.Equals(
                    candidateKey.Predicate,
                    conditions[0].Predicate.Value,
                    StringComparison.Ordinal))
            {
                continue;
            }

            var combinations = RulePremiseMatcher.GetAdditionalPremiseCombinations(
                conditions.Skip(1),
                candidateKey.Individual,
                knownFacts);

            foreach (var combination in combinations)
            {
                var additionalPremises = candidate.AdditionalPremises.IsDefault
                    ? ImmutableArray<ProofNode>.Empty
                    : candidate.AdditionalPremises;

                ProofOperations.AddUniqueProof(
                    proofs,
                    candidate with
                    {
                        AdditionalPremises = additionalPremises.AddRange(combination)
                    });
            }
        }

        return ProofOperations.CreateResult(proofs);
    }

    private static bool IsApplicableUniversalRule(
        UniversalRule rule,
        UniversalRule query)
    {
        if (rule.ConclusionPolarity is not (EvidencePolarity.Positive or EvidencePolarity.Negative) ||
            rule.Conclusion.Arguments.IsDefault ||
            rule.Conclusion.Arguments.Length != 1 ||
            !IsSamePredicate(rule.Conclusion, query.Conclusion) ||
            rule.Premises.IsDefaultOrEmpty ||
            !rule.NegativePremises.IsDefaultOrEmpty ||
            !rule.RelationalPremises.IsDefaultOrEmpty ||
            !rule.NegativeRelationalPremises.IsDefaultOrEmpty)
        {
            return false;
        }

        return rule.Premises.All(premise =>
            query.Premises.Any(queryPremise => IsSamePredicate(premise, queryPremise)));
    }

    private static ProofNode CreateUniversalRuleProof(UniversalRule rule) =>
        new(
            rule.Conclusion,
            rule.SourceText,
            null,
            SourceKind: rule.SourceKind,
            AdditionalSources: rule.AdditionalSources,
            SourceId: rule.SourceId)
        {
            Polarity = rule.ConclusionPolarity
        };

    private static IEnumerable<ProofNode> FindUniversalCounterexamples(
        KnowledgeBase knowledgeBase,
        UniversalRule query)
    {
        var positiveFacts = FactDeriver.DeriveUnaryFacts(knowledgeBase);
        var negativeFacts = FactDeriver.DeriveNegativeUnaryFacts(knowledgeBase);
        var firstPremise = query.Premises[0];
        var contradictingProofs = new List<ProofNode>();

        foreach (var classProof in positiveFacts)
        {
            if (!ProofOperations.TryGetFactKey(classProof.Predicate, out var classKey) ||
                !string.Equals(
                    classKey.Predicate,
                    firstPremise.Predicate.Value,
                    StringComparison.Ordinal))
            {
                continue;
            }

            var additionalPremiseCombinations = RulePremiseMatcher.GetAdditionalPremiseCombinations(
                query.Premises.Skip(1),
                classKey.Individual,
                positiveFacts);

            foreach (var additionalPremises in additionalPremiseCombinations)
            {
                var negativeConclusions = negativeFacts.Where(proof =>
                    ProofOperations.TryGetFactKey(proof.Predicate, out var key) &&
                    string.Equals(
                        key.Predicate,
                        query.Conclusion.Predicate.Value,
                        StringComparison.Ordinal) &&
                    key.Individual == classKey.Individual);

                foreach (var negativeConclusion in negativeConclusions)
                {
                    var existingPremises = negativeConclusion.AdditionalPremises.IsDefault
                        ? ImmutableArray<ProofNode>.Empty
                        : negativeConclusion.AdditionalPremises;
                    var counterexample = negativeConclusion with
                    {
                        AdditionalPremises = existingPremises
                            .Add(classProof)
                            .AddRange(additionalPremises),
                        Kind = ProofNodeKind.Derived
                    };

                    ProofOperations.AddUniqueProof(contradictingProofs, counterexample);
                }
            }
        }

        return contradictingProofs;
    }

    private static bool IsSamePredicate(
        PredicateApplication left,
        PredicateApplication right) =>
        left.Predicate == right.Predicate &&
        !left.Arguments.IsDefault &&
        !right.Arguments.IsDefault &&
        left.Arguments.SequenceEqual(right.Arguments);

    private static bool TryGetBoundUnaryPredicates(
        SemanticFormula formula,
        VariableTerm variable,
        out ImmutableArray<PredicateApplication> predicates)
    {
        predicates = formula switch
        {
            PredicateApplication predicate => ImmutableArray.Create(predicate),
            Conjunction conjunction when !conjunction.Conjuncts.IsDefaultOrEmpty =>
                conjunction.Conjuncts
                    .OfType<PredicateApplication>()
                    .ToImmutableArray(),
            _ => ImmutableArray<PredicateApplication>.Empty
        };

        if (predicates.IsDefaultOrEmpty ||
            (formula is Conjunction compound &&
             predicates.Length != compound.Conjuncts.Length))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(variable.Identifier.Value) &&
            predicates.All(predicate =>
                !string.IsNullOrWhiteSpace(predicate.Predicate.Value) &&
                !predicate.Arguments.IsDefault &&
                predicate.Arguments.Length == 1 &&
                predicate.Arguments[0] is VariableTerm argument &&
                argument.Identifier == variable.Identifier);
    }

    private static bool TryGroundQuery(
        PredicateApplication predicate,
        out PredicateApplication groundedPredicate)
    {
        groundedPredicate = predicate;

        if (string.IsNullOrWhiteSpace(predicate.Predicate.Value) ||
            predicate.Arguments.IsDefaultOrEmpty)
        {
            return false;
        }

        var arguments = ImmutableArray.CreateBuilder<SemanticTerm>(predicate.Arguments.Length);

        foreach (var argument in predicate.Arguments)
        {
            switch (argument)
            {
                case IndividualTerm individual when
                    !string.IsNullOrWhiteSpace(individual.Identifier.Value):
                    arguments.Add(individual);
                    break;

                case AnonymousIndividualTerm anonymous when
                    !string.IsNullOrWhiteSpace(anonymous.Identity.Value):
                    arguments.Add(anonymous);
                    break;

                case DescriptiveIndividualTerm description
                    when !string.IsNullOrWhiteSpace(description.Head.Value) &&
                         !description.Modifiers.IsDefault &&
                         description.Modifiers.All(modifier =>
                             !string.IsNullOrWhiteSpace(modifier.Value)):
                    arguments.Add(new IndividualTerm(description.Identifier));
                    break;

                default:
                    return false;
            }
        }

        groundedPredicate = predicate with
        {
            Arguments = arguments.ToImmutable()
        };

        return true;
    }


}
