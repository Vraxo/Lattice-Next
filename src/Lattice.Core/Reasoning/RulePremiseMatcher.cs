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
        IReadOnlyList<ProofNode> positiveRelationalFacts,
        IReadOnlyList<ProofNode> negativeRelationalFacts)
    {
        var positivePremises = GetAdditionalPremiseCombinations(rule.Premises, individual, positiveFacts);
        var negativePremises = GetAdditionalPremiseCombinations(
            rule.NegativePremises.IsDefault ? ImmutableArray<PredicateApplication>.Empty : rule.NegativePremises,
            individual, negativeFacts);
        var relationPremises = GetRelationalPremiseCombinations(
            rule.RelationalPremises.IsDefault ? ImmutableArray<PredicateApplication>.Empty : rule.RelationalPremises,
            individual, positiveRelationalFacts);
        var negativeRelationPremises = GetRelationalPremiseCombinations(
            rule.NegativeRelationalPremises.IsDefault ? ImmutableArray<PredicateApplication>.Empty : rule.NegativeRelationalPremises,
            individual, negativeRelationalFacts);
        var existentialPremises = GetExistentialPremiseCombinations(
            rule.ExistentialPremises.IsDefault ? ImmutableArray<ExistentialRulePremise>.Empty : rule.ExistentialPremises,
            individual,
            positiveFacts,
            negativeFacts,
            positiveRelationalFacts);
        var combinations = new List<ImmutableArray<ProofNode>>(
            positivePremises.Count *
            negativePremises.Count *
            relationPremises.Count *
            negativeRelationPremises.Count *
            existentialPremises.Count);

        foreach (var positive in positivePremises)
        foreach (var negative in negativePremises)
        foreach (var relation in relationPremises)
        foreach (var negativeRelation in negativeRelationPremises)
        foreach (var existential in existentialPremises)
        {
            combinations.Add(
                positive
                    .AddRange(negative)
                    .AddRange(relation)
                    .AddRange(negativeRelation)
                    .AddRange(existential));
        }

        return combinations;
    }

    private static List<ImmutableArray<ProofNode>> GetExistentialPremiseCombinations(
        IEnumerable<ExistentialRulePremise> requiredPremises,
        SemanticTerm individual,
        IReadOnlyList<ProofNode> knownUnaryFacts,
        IReadOnlyList<ProofNode> knownNegativeUnaryFacts,
        IReadOnlyList<ProofNode> knownRelationalFacts)
    {
        var premiseCombinations = new List<ImmutableArray<ProofNode>>
        {
            ImmutableArray<ProofNode>.Empty
        };

        foreach (var required in requiredPremises)
        {
            var matchedPremises = GetExistentialWitnessCombinations(
                required,
                individual,
                knownUnaryFacts,
                knownNegativeUnaryFacts,
                knownRelationalFacts);
            if (matchedPremises.Count == 0)
            {
                return [];
            }

            var expanded = new List<ImmutableArray<ProofNode>>(
                premiseCombinations.Count * matchedPremises.Count);
            foreach (var existing in premiseCombinations)
            foreach (var matched in matchedPremises)
            {
                expanded.Add(existing.AddRange(matched));
            }

            premiseCombinations = expanded;
        }

        return premiseCombinations;
    }

    private static List<ImmutableArray<ProofNode>> GetExistentialWitnessCombinations(
        ExistentialRulePremise required,
        SemanticTerm subject,
        IReadOnlyList<ProofNode> knownUnaryFacts,
        IReadOnlyList<ProofNode> knownNegativeUnaryFacts,
        IReadOnlyList<ProofNode> knownRelationalFacts)
    {
        var witnessMatches = new List<(SemanticTerm Witness, ImmutableArray<ProofNode> Relations)>();
        foreach (var relation in required.Relations)
        {
            if (relation.Arguments.IsDefault ||
                relation.Arguments.Length != 2 ||
                relation.Arguments[0] is not VariableTerm ||
                relation.Arguments[1] is not VariableTerm witnessVariable ||
                witnessVariable.Identifier != required.Witness.Identifier)
            {
                return [];
            }

            var matches = knownRelationalFacts.Where(proof =>
                proof.Predicate.Predicate == relation.Predicate &&
                !proof.Predicate.Arguments.IsDefault &&
                proof.Predicate.Arguments.Length == 2 &&
                proof.Predicate.Arguments[0] == subject).ToArray();

            if (matches.Length == 0)
            {
                return [];
            }

            if (witnessMatches.Count == 0)
            {
                witnessMatches.AddRange(matches.Select(match =>
                    (match.Predicate.Arguments[1], ImmutableArray.Create(match))));
                continue;
            }

            var joined = new List<(SemanticTerm Witness, ImmutableArray<ProofNode> Relations)>();
            foreach (var existing in witnessMatches)
            foreach (var match in matches)
            {
                if (match.Predicate.Arguments[1] == existing.Witness)
                {
                    joined.Add((existing.Witness, existing.Relations.Add(match)));
                }
            }

            witnessMatches = joined;
            if (witnessMatches.Count == 0)
            {
                return [];
            }
        }

        foreach (var fixedRelation in required.FixedObjectRelations)
        {
            if (fixedRelation.Arguments.IsDefault ||
                fixedRelation.Arguments.Length != 2 ||
                fixedRelation.Arguments[0] is not VariableTerm witnessVariable ||
                witnessVariable.Identifier != required.Witness.Identifier ||
                fixedRelation.Arguments[1] is not IndividualTerm fixedObject)
            {
                return [];
            }

            var expandedWitnesses = new List<(SemanticTerm Witness, ImmutableArray<ProofNode> Relations)>();
            foreach (var witness in witnessMatches)
            {
                var matches = knownRelationalFacts.Where(proof =>
                    proof.Predicate.Predicate == fixedRelation.Predicate &&
                    !proof.Predicate.Arguments.IsDefault &&
                    proof.Predicate.Arguments.Length == 2 &&
                    proof.Predicate.Arguments[0] == witness.Witness &&
                    proof.Predicate.Arguments[1] == fixedObject).ToArray();

                foreach (var match in matches)
                {
                    expandedWitnesses.Add((witness.Witness, witness.Relations.Add(match)));
                }
            }

            witnessMatches = expandedWitnesses;
            if (witnessMatches.Count == 0)
            {
                return [];
            }
        }

        foreach (var variableRelation in required.VariableRelations)
        {
            if (variableRelation.Arguments.IsDefault ||
                variableRelation.Arguments.Length != 2 ||
                variableRelation.Arguments[0] is not VariableTerm witnessVariable ||
                witnessVariable.Identifier != required.Witness.Identifier ||
                variableRelation.Arguments[1] is not VariableTerm)
            {
                return [];
            }

            var expandedWitnesses = new List<(SemanticTerm Witness, ImmutableArray<ProofNode> Relations)>();
            foreach (var witness in witnessMatches)
            {
                var matches = knownRelationalFacts.Where(proof =>
                    proof.Predicate.Predicate == variableRelation.Predicate &&
                    !proof.Predicate.Arguments.IsDefault &&
                    proof.Predicate.Arguments.Length == 2 &&
                    proof.Predicate.Arguments[0] == witness.Witness &&
                    proof.Predicate.Arguments[1] == subject).ToArray();

                foreach (var match in matches)
                {
                    expandedWitnesses.Add((witness.Witness, witness.Relations.Add(match)));
                }
            }

            witnessMatches = expandedWitnesses;
            if (witnessMatches.Count == 0)
            {
                return [];
            }
        }

        var matchedPremises = new List<ImmutableArray<ProofNode>>();
        foreach (var witness in witnessMatches)
        {
            var conditionCombinations = GetAdditionalPremiseCombinations(
                required.Conditions,
                witness.Witness,
                knownUnaryFacts);
            var negativeConditionCombinations = GetAdditionalPremiseCombinations(
                required.NegativeConditions,
                witness.Witness,
                knownNegativeUnaryFacts);
            var nestedCombinations = GetExistentialPremiseCombinations(
                required.NestedPremises,
                witness.Witness,
                knownUnaryFacts,
                knownNegativeUnaryFacts,
                knownRelationalFacts);

            foreach (var conditions in conditionCombinations)
            foreach (var negativeConditions in negativeConditionCombinations)
            foreach (var nested in nestedCombinations)
            {
                matchedPremises.Add(
                    witness.Relations
                        .AddRange(conditions)
                        .AddRange(negativeConditions)
                        .AddRange(nested));
            }
        }

        return matchedPremises;
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
                required.Arguments[1] is not IndividualTerm objectTerm)
            {
                return [];
            }

            var matches = knownFacts.Where(proof =>
                proof.Predicate.Predicate == required.Predicate &&
                !proof.Predicate.Arguments.IsDefault && proof.Predicate.Arguments.Length == 2 &&
                proof.Predicate.Arguments[0] == individual && proof.Predicate.Arguments[1] == objectTerm).ToArray();

            if (matches.Length == 0)
            {
                return [];
            }

            var expanded = new List<ImmutableArray<ProofNode>>(combinations.Count * matches.Length);
            foreach (var combination in combinations)
            foreach (var match in matches)
            {
                expanded.Add(combination.Add(match));
            }
            combinations = expanded;
        }
        return combinations;
    }

    internal static List<ImmutableArray<ProofNode>> GetAdditionalPremiseCombinations(
        IEnumerable<PredicateApplication> requiredPremises,
        SemanticTerm individual,
        IReadOnlyList<ProofNode> knownFacts)
    {
        var combinations = new List<ImmutableArray<ProofNode>> { ImmutableArray<ProofNode>.Empty };
        foreach (var requiredPremise in requiredPremises)
        {
            var matchingProofs = knownFacts.Where(proof =>
                ProofOperations.TryGetFactKey(proof.Predicate, out var key) &&
                string.Equals(key.Predicate, requiredPremise.Predicate.Value, StringComparison.Ordinal) &&
                key.Individual == individual).ToArray();
            if (matchingProofs.Length == 0)
            {
                return [];
            }

            var expanded = new List<ImmutableArray<ProofNode>>(combinations.Count * matchingProofs.Length);
            foreach (var combination in combinations)
            foreach (var matchingProof in matchingProofs)
            {
                expanded.Add(combination.Add(matchingProof));
            }
            combinations = expanded;
        }
        return combinations;
    }
}
