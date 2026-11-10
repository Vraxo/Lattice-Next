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
        if (!TryGroundQuery(query, out var groundedQuery)) return new ReasoningResult(null);
        query = groundedQuery;

        var negativeProofs = knowledgeBase.Facts
            .Where(fact => fact.IsNegated && IsSameGroundPredicate(fact.Predicate, query))
            .Select(CreateAssertedProof).ToArray();

        if (query.Arguments.Length == 2)
        {
            var proofs = knowledgeBase.Facts
                .Where(fact => !fact.IsNegated && IsSameGroundPredicate(fact.Predicate, query))
                .Select(CreateAssertedProof).ToList();
            proofs.AddRange(DeriveRelationalFacts(knowledgeBase).Where(candidate => IsSameGroundPredicate(candidate.Predicate, query)));
            return CreateResult(proofs, negativeProofs);
        }

        if (query.Arguments.Length != 1 || !TryGetFactKey(query, out var queryKey))
        {
            var directProofs = knowledgeBase.Facts
                .Where(fact => !fact.IsNegated && IsSameGroundPredicate(fact.Predicate, query))
                .Select(CreateAssertedProof);
            return CreateResult(directProofs, negativeProofs);
        }

        var matchingProofs = DeriveUnaryFacts(knowledgeBase).Where(candidate =>
            TryGetFactKey(candidate.Predicate, out var key) && key == queryKey);
        return CreateResult(matchingProofs, negativeProofs);
    }

    public ReasoningResult Ask(KnowledgeBase knowledgeBase, Negation query)
    {
        ArgumentNullException.ThrowIfNull(knowledgeBase);
        ArgumentNullException.ThrowIfNull(query);
        if (query.Formula is not PredicateApplication predicate) return new ReasoningResult(null);
        var result = Ask(knowledgeBase, predicate);
        return new ReasoningResult(result.NegativeProofs.FirstOrDefault())
        {
            AdditionalProofs = result.NegativeProofs.Skip(1).ToImmutableArray(),
            NegativeProof = result.Proofs.FirstOrDefault(),
            AdditionalNegativeProofs = result.Proofs.Skip(1).ToImmutableArray()
        };
    }

    private static ProofNode CreateAssertedProof(KnowledgeFact fact) =>
        new(fact.Predicate, fact.SourceText, null, fact.ResolvedReferent,
            SourceKind: fact.SourceKind, AdditionalSources: fact.AdditionalSources, SourceId: fact.SourceId)
        {
            IsNegated = fact.IsNegated
        };

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

        var knownFacts = DeriveUnaryFacts(knowledgeBase);
        var proofs = new List<ProofNode>();

        foreach (var candidate in knownFacts)
        {
            if (!TryGetFactKey(candidate.Predicate, out var candidateKey) ||
                !string.Equals(
                    candidateKey.Predicate,
                    conditions[0].Predicate.Value,
                    StringComparison.Ordinal))
            {
                continue;
            }

            var combinations = GetAdditionalPremiseCombinations(
                conditions.Skip(1),
                candidateKey.Individual,
                knownFacts);

            foreach (var combination in combinations)
            {
                var additionalPremises = candidate.AdditionalPremises.IsDefault
                    ? ImmutableArray<ProofNode>.Empty
                    : candidate.AdditionalPremises;

                AddUniqueProof(
                    proofs,
                    candidate with
                    {
                        AdditionalPremises = additionalPremises.AddRange(combination)
                    });
            }
        }

        return CreateResult(proofs);
    }

    private static List<ProofNode> DeriveUnaryFacts(KnowledgeBase knowledgeBase)
    {
        var knownFacts = new List<ProofNode>();
        var proofsByFact = new Dictionary<(string Predicate, SemanticTerm Individual), List<ProofNode>>();

        foreach (var fact in knowledgeBase.Facts.Where(fact => !fact.IsNegated))
        {
            TryAddProof(
                knownFacts,
                proofsByFact,
                new ProofNode(
                    fact.Predicate,
                    fact.SourceText,
                    null,
                    fact.ResolvedReferent,
                    SourceKind: fact.SourceKind,
                    AdditionalSources: fact.AdditionalSources,
                    SourceId: fact.SourceId));
        }

        bool added;

        do
        {
            added = false;

            foreach (var rule in knowledgeBase.Rules)
            {
                if (rule.Premises.IsDefaultOrEmpty ||
                    rule.Conclusion.Arguments.IsDefault ||
                    rule.Conclusion.Arguments.Length != 1)
                {
                    continue;
                }

                var candidatePremises = knownFacts
                    .Where(proof =>
                        TryGetFactKey(proof.Predicate, out var key) &&
                        string.Equals(
                            key.Predicate,
                            rule.Premises[0].Predicate.Value,
                            StringComparison.Ordinal))
                    .ToArray();

                foreach (var candidatePremise in candidatePremises)
                {
                    if (!TryGetFactKey(candidatePremise.Predicate, out var candidateKey))
                    {
                        continue;
                    }

                    var combinations = GetAdditionalPremiseCombinations(
                        rule.Premises.Skip(1),
                        candidateKey.Individual,
                        knownFacts);

                    foreach (var combination in combinations)
                    {
                        var premises = ImmutableArray.Create(candidatePremise)
                            .AddRange(combination);
                        var conclusion = CreateGroundPredicate(
                            rule.Conclusion.Predicate,
                            candidateKey.Individual);

                        if (!TryGetFactKey(conclusion, out var conclusionKey) ||
                            premises.Any(premise => ContainsFact(premise, conclusionKey)))
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
                            SourceId: rule.SourceId);

                        if (TryAddProof(knownFacts, proofsByFact, proof))
                        {
                            added = true;
                        }
                    }
                }
            }
        }
        while (added);

        return knownFacts;
    }

    private static List<ProofNode> DeriveRelationalFacts(KnowledgeBase knowledgeBase)
    {
        var unaryFacts = DeriveUnaryFacts(knowledgeBase);
        var relationalProofs = new List<ProofNode>();

        foreach (var rule in knowledgeBase.Rules)
        {
            if (rule.Premises.IsDefaultOrEmpty ||
                rule.Conclusion.Arguments.IsDefault ||
                rule.Conclusion.Arguments.Length != 2 ||
                rule.Conclusion.Arguments[0] is not VariableTerm ||
                rule.Conclusion.Arguments[1] is not IndividualTerm objectTerm ||
                string.IsNullOrWhiteSpace(objectTerm.Identifier.Value))
            {
                continue;
            }

            var candidatePremises = unaryFacts
                .Where(proof =>
                    TryGetFactKey(proof.Predicate, out var key) &&
                    string.Equals(
                        key.Predicate,
                        rule.Premises[0].Predicate.Value,
                        StringComparison.Ordinal))
                .ToArray();

            foreach (var candidatePremise in candidatePremises)
            {
                if (!TryGetFactKey(candidatePremise.Predicate, out var candidateKey))
                {
                    continue;
                }

                var combinations = GetAdditionalPremiseCombinations(
                    rule.Premises.Skip(1),
                    candidateKey.Individual,
                    unaryFacts);

                foreach (var combination in combinations)
                {
                    var conclusion = new PredicateApplication(
                        rule.Conclusion.Predicate,
                        ImmutableArray.Create<SemanticTerm>(
                            candidateKey.Individual,
                            objectTerm));
                    var premises = ImmutableArray.Create(candidatePremise)
                        .AddRange(combination);

                    AddUniqueProof(
                        relationalProofs,
                        new ProofNode(
                            conclusion,
                            rule.SourceText,
                            premises[0],
                            AdditionalPremises: premises.Skip(1).ToImmutableArray(),
                            SourceKind: rule.SourceKind,
                            AdditionalSources: rule.AdditionalSources,
                            Kind: ProofNodeKind.Derived,
                            SourceId: rule.SourceId));
                }
            }
        }

        return relationalProofs;
    }

    private static List<ImmutableArray<ProofNode>> GetAdditionalPremiseCombinations(
        IEnumerable<PredicateApplication> requiredPremises,
        SemanticTerm individual,
        IReadOnlyList<ProofNode> knownFacts)
    {
        var combinations = new List<ImmutableArray<ProofNode>>
        {
            ImmutableArray<ProofNode>.Empty
        };

        foreach (var requiredPremise in requiredPremises)
        {
            var matchingProofs = knownFacts
                .Where(proof =>
                    TryGetFactKey(proof.Predicate, out var key) &&
                    string.Equals(
                        key.Predicate,
                        requiredPremise.Predicate.Value,
                        StringComparison.Ordinal) &&
                    key.Individual == individual)
                .ToArray();

            if (matchingProofs.Length == 0)
            {
                return [];
            }

            var expanded = new List<ImmutableArray<ProofNode>>(
                combinations.Count * matchingProofs.Length);

            foreach (var combination in combinations)
            {
                foreach (var matchingProof in matchingProofs)
                {
                    expanded.Add(combination.Add(matchingProof));
                }
            }

            combinations = expanded;
        }

        return combinations;
    }

    private static bool TryAddProof(
        List<ProofNode> knownFacts,
        Dictionary<(string Predicate, SemanticTerm Individual), List<ProofNode>> proofsByFact,
        ProofNode proof)
    {
        if (!TryGetFactKey(proof.Predicate, out var key))
        {
            return false;
        }

        if (!proofsByFact.TryGetValue(key, out var existingProofs))
        {
            existingProofs = [];
            proofsByFact.Add(key, existingProofs);
        }

        if (existingProofs.Any(existing => AreSameProof(existing, proof)))
        {
            return false;
        }

        existingProofs.Add(proof);
        knownFacts.Add(proof);
        return true;
    }

    private static void AddUniqueProof(
        List<ProofNode> proofs,
        ProofNode proof)
    {
        if (!proofs.Any(existing => AreSameProof(existing, proof)))
        {
            proofs.Add(proof);
        }
    }

    private static ReasoningResult CreateResult(
        IEnumerable<ProofNode> candidateProofs,
        IEnumerable<ProofNode>? candidateNegativeProofs = null)
    {
        var proofs = new List<ProofNode>();

        foreach (var proof in candidateProofs)
        {
            AddUniqueProof(proofs, proof);
        }

        var negativeProofs = new List<ProofNode>();
        foreach (var proof in candidateNegativeProofs ?? [])
        {
            AddUniqueProof(negativeProofs, proof);
        }

        return new ReasoningResult(proofs.FirstOrDefault())
        {
            AdditionalProofs = proofs.Skip(1).ToImmutableArray(),
            NegativeProof = negativeProofs.FirstOrDefault(),
            AdditionalNegativeProofs = negativeProofs.Skip(1).ToImmutableArray()
        };
    }

    private static bool AreSameProof(ProofNode left, ProofNode right)
    {
        if (!IsSameGroundPredicate(left.Predicate, right.Predicate) ||
            !string.Equals(left.SourceText, right.SourceText, StringComparison.Ordinal) ||
            left.ResolvedReferent != right.ResolvedReferent ||
            left.Kind != right.Kind ||
            left.IsNegated != right.IsNegated ||
            !left.Sources.SequenceEqual(right.Sources) ||
            (left.Premise is null) != (right.Premise is null))
        {
            return false;
        }

        if (left.Premise is not null &&
            !AreSameProof(left.Premise, right.Premise!))
        {
            return false;
        }

        var leftAdditional = left.AdditionalPremises.IsDefault
            ? ImmutableArray<ProofNode>.Empty
            : left.AdditionalPremises;
        var rightAdditional = right.AdditionalPremises.IsDefault
            ? ImmutableArray<ProofNode>.Empty
            : right.AdditionalPremises;

        return leftAdditional.Length == rightAdditional.Length &&
            leftAdditional.Zip(rightAdditional).All(pair =>
                AreSameProof(pair.First, pair.Second));
    }

    private static bool ContainsFact(
        ProofNode proof,
        (string Predicate, SemanticTerm Individual) key)
    {
        if (TryGetFactKey(proof.Predicate, out var proofKey) &&
            proofKey == key)
        {
            return true;
        }

        if (proof.Premise is not null && ContainsFact(proof.Premise, key))
        {
            return true;
        }

        if (!proof.AdditionalPremises.IsDefaultOrEmpty)
        {
            return proof.AdditionalPremises.Any(premise => ContainsFact(premise, key));
        }

        return false;
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

    private static bool IsSameGroundPredicate(
        PredicateApplication left,
        PredicateApplication right) =>
        left.Predicate == right.Predicate &&
        !left.Arguments.IsDefault &&
        !right.Arguments.IsDefault &&
        left.Arguments.SequenceEqual(right.Arguments);

    private static bool TryGetFactKey(
        PredicateApplication predicate,
        out (string Predicate, SemanticTerm Individual) key)
    {
        key = (string.Empty, null!);

        if (string.IsNullOrWhiteSpace(predicate.Predicate.Value) ||
            predicate.Arguments.IsDefault ||
            predicate.Arguments.Length != 1)
        {
            return false;
        }

        var individual = predicate.Arguments[0];

        var hasValidIdentity = individual switch
        {
            IndividualTerm named => !string.IsNullOrWhiteSpace(named.Identifier.Value),
            AnonymousIndividualTerm anonymous => !string.IsNullOrWhiteSpace(anonymous.Identity.Value),
            _ => false
        };

        if (!hasValidIdentity)
        {
            return false;
        }

        key = (predicate.Predicate.Value, individual);
        return true;
    }

    private static PredicateApplication CreateGroundPredicate(
        SemanticIdentifier predicate,
        SemanticTerm individual) =>
        new(
            predicate,
            ImmutableArray.Create(individual));
}
