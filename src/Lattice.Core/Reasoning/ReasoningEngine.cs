using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

public sealed class ReasoningEngine
{
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
                .Select(CreateAssertedProof)
                .Concat(FactDeriver.DeriveRelationalFacts(knowledgeBase, EvidencePolarity.Positive).Where(candidate =>
                    ProofOperations.IsSameGroundPredicate(candidate.Predicate, query)));
        }
        else if (query.Arguments.Length == 1 &&
            ProofOperations.TryGetFactKey(query, out var queryKey))
        {
            positiveProofs = FactDeriver.DeriveUnaryFacts(knowledgeBase).Where(candidate =>
                ProofOperations.TryGetFactKey(candidate.Predicate, out var key) &&
                key == queryKey);
        }
        else
        {
            positiveProofs = knowledgeBase.Facts
                .Where(fact =>
                    fact.Polarity == EvidencePolarity.Positive &&
                    ProofOperations.IsSameGroundPredicate(fact.Predicate, query))
                .Select(CreateAssertedProof);
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
            .Select(CreateAssertedProof)
            .Concat(negativeDerivedProofs.Where(candidate =>
                ProofOperations.IsSameGroundPredicate(candidate.Predicate, query)));

        return ProofOperations.CreateResult(positiveProofs, negativeProofs);
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
