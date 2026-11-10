using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

internal static class ProofOperations
{
    internal static bool TryAddProof(
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

    internal static void AddUniqueProof(
        List<ProofNode> proofs,
        ProofNode proof)
    {
        if (!proofs.Any(existing => AreSameProof(existing, proof)))
        {
            proofs.Add(proof);
        }
    }

    internal static ProofNode CreateAssertedProof(KnowledgeFact fact) =>
        new(
            fact.Predicate,
            fact.SourceText,
            null,
            fact.ResolvedReferent,
            SourceKind: fact.SourceKind,
            AdditionalSources: fact.AdditionalSources,
            SourceId: fact.SourceId)
        {
            Polarity = fact.Polarity
        };

    internal static ReasoningResult CreateResult(
        IEnumerable<ProofNode> supportingProofs,
        IEnumerable<ProofNode>? contradictingProofs = null)
    {
        var supporting = new List<ProofNode>();

        foreach (var proof in supportingProofs)
        {
            AddUniqueProof(supporting, proof);
        }

        var contradicting = new List<ProofNode>();

        if (contradictingProofs is not null)
        {
            foreach (var proof in contradictingProofs)
            {
                AddUniqueProof(contradicting, proof);
            }
        }

        return new ReasoningResult(supporting.FirstOrDefault())
        {
            AdditionalProofs = supporting.Skip(1).ToImmutableArray(),
            ContradictingProofs = contradicting.ToImmutableArray()
        };
    }

    internal static bool AreSameProof(ProofNode left, ProofNode right)
    {
        if (!IsSameGroundPredicate(left.Predicate, right.Predicate) ||
            !string.Equals(left.SourceText, right.SourceText, StringComparison.Ordinal) ||
            left.ResolvedReferent != right.ResolvedReferent ||
            left.Kind != right.Kind ||
            left.Polarity != right.Polarity ||
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

    internal static bool ContainsFact(
        ProofNode proof,
        (string Predicate, SemanticTerm Individual) key,
        EvidencePolarity polarity)
    {
        if (proof.Polarity == polarity &&
            TryGetFactKey(proof.Predicate, out var proofKey) &&
            proofKey == key)
        {
            return true;
        }

        if (proof.Premise is not null && ContainsFact(proof.Premise, key, polarity))
        {
            return true;
        }

        if (!proof.AdditionalPremises.IsDefaultOrEmpty)
        {
            return proof.AdditionalPremises.Any(premise => ContainsFact(premise, key, polarity));
        }

        return false;
    }

    internal static bool IsSameGroundPredicate(
        PredicateApplication left,
        PredicateApplication right) =>
        left.Predicate == right.Predicate &&
        !left.Arguments.IsDefault &&
        !right.Arguments.IsDefault &&
        left.Arguments.SequenceEqual(right.Arguments);

    internal static bool TryGetFactKey(
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

    internal static PredicateApplication CreateGroundPredicate(
        SemanticIdentifier predicate,
        SemanticTerm individual) =>
        new(
            predicate,
            ImmutableArray.Create(individual));
}
