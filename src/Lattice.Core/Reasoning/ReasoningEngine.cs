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

        var directFact = knowledgeBase.Facts.FirstOrDefault(fact =>
            IsSameGroundPredicate(fact.Predicate, query));

        if (directFact is not null)
        {
            return new ReasoningResult(new ProofNode(
                directFact.Predicate,
                directFact.SourceText,
                null,
                directFact.ResolvedReferent));
        }

        if (!TryGetFactKey(query, out var queryKey))
        {
            return new ReasoningResult(null);
        }

        var knownFacts = new List<ProofNode>();
        var knownKeys = new HashSet<(string Predicate, string Individual)>();

        foreach (var fact in knowledgeBase.Facts)
        {
            if (TryGetFactKey(fact.Predicate, out var key) &&
                knownKeys.Add(key))
            {
                knownFacts.Add(new ProofNode(
                    fact.Predicate,
                    fact.SourceText,
                    null,
                    fact.ResolvedReferent));
            }
        }

        bool added;

        do
        {
            added = false;

            foreach (var rule in knowledgeBase.Rules)
            {
                var matchingPremises = knownFacts
                    .Where(proof =>
                        TryGetFactKey(proof.Predicate, out var key) &&
                        string.Equals(
                            key.Predicate,
                            rule.Premise.Predicate.Value,
                            StringComparison.Ordinal))
                    .ToArray();

                foreach (var premiseProof in matchingPremises)
                {
                    var individual = (IndividualTerm)premiseProof.Predicate.Arguments[0];
                    var conclusion = CreateGroundPredicate(
                        rule.Conclusion.Predicate,
                        individual.Identifier);

                    if (!TryGetFactKey(conclusion, out var conclusionKey) ||
                        !knownKeys.Add(conclusionKey))
                    {
                        continue;
                    }

                    knownFacts.Add(new ProofNode(
                        conclusion,
                        rule.SourceText,
                        premiseProof));

                    added = true;
                }
            }
        }
        while (added);

        var proof = knownFacts.FirstOrDefault(candidate =>
            TryGetFactKey(candidate.Predicate, out var key) &&
            key == queryKey);

        return new ReasoningResult(proof);
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
        out (string Predicate, string Individual) key)
    {
        key = (string.Empty, string.Empty);

        if (string.IsNullOrWhiteSpace(predicate.Predicate.Value) ||
            predicate.Arguments.IsDefault ||
            predicate.Arguments.Length != 1 ||
            predicate.Arguments[0] is not IndividualTerm individual ||
            string.IsNullOrWhiteSpace(individual.Identifier.Value))
        {
            return false;
        }

        key = (predicate.Predicate.Value, individual.Identifier.Value);
        return true;
    }

    private static PredicateApplication CreateGroundPredicate(
        SemanticIdentifier predicate,
        SemanticIdentifier individual) =>
        new(
            predicate,
            ImmutableArray.Create<SemanticTerm>(new IndividualTerm(individual)));
}
