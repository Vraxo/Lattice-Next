using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

internal static class RulePremiseMatcher
{
    internal static List<ImmutableArray<ProofNode>> GetRulePremiseCombinations(
        UniversalRule rule,
        SemanticTerm individual,
        IReadOnlyList<ProofNode> positiveFacts,
        IReadOnlyList<ProofNode> negativeFacts,
        IReadOnlyList<ProofNode> relationalFacts)
    {
        var positivePremises = GetAdditionalPremiseCombinations(
            rule.Premises,
            individual,
            positiveFacts);
        var negativePremises = GetAdditionalPremiseCombinations(
            rule.NegativePremises.IsDefault
                ? ImmutableArray<PredicateApplication>.Empty
                : rule.NegativePremises,
            individual,
            negativeFacts);
        var relationPremises = GetRelationalPremiseCombinations(
            rule.RelationalPremises.IsDefault ? ImmutableArray<PredicateApplication>.Empty : rule.RelationalPremises,
            individual, relationalFacts);
        var combinations = new List<ImmutableArray<ProofNode>>(
            positivePremises.Count * negativePremises.Count * relationPremises.Count);
        foreach (var positive in positivePremises)
        foreach (var negative in negativePremises)
        foreach (var relation in relationPremises)
            combinations.Add(positive.AddRange(negative).AddRange(relation));
        return combinations;
    }

    private static List<ImmutableArray<ProofNode>> GetRelationalPremiseCombinations(
        IEnumerable<PredicateApplication> requiredPremises,
        SemanticTerm individual,
        IReadOnlyList<ProofNode> knownFacts)
    {
        var combinations = new List<ImmutableArray<ProofNode>> { ImmutableArray<ProofNode>.Empty };
        foreach (var required in requiredPremises)
        {
            if (required.Arguments.IsDefault || required.Arguments.Length != 2 ||
                required.Arguments[1] is not IndividualTerm objectTerm) return [];
            var matches = knownFacts.Where(proof =>
                proof.Predicate.Predicate == required.Predicate &&
                !proof.Predicate.Arguments.IsDefault && proof.Predicate.Arguments.Length == 2 &&
                proof.Predicate.Arguments[0] == individual && proof.Predicate.Arguments[1] == objectTerm).ToArray();
            if (matches.Length == 0) return [];
            var expanded = new List<ImmutableArray<ProofNode>>(combinations.Count * matches.Length);
            foreach (var combination in combinations)
            foreach (var match in matches) expanded.Add(combination.Add(match));
            combinations = expanded;
        }
        return combinations;
    }

    internal static List<ImmutableArray<ProofNode>> GetAdditionalPremiseCombinations(
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
                    ProofOperations.TryGetFactKey(proof.Predicate, out var key) &&
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


}
