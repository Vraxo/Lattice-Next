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
                directFact.ResolvedReferent,
                SourceKind: directFact.SourceKind,
                AdditionalSources: directFact.AdditionalSources,
                SourceId: directFact.SourceId));
        }

        if (query.Arguments.Length == 2)
        {
            var relationalProof = DeriveRelationalFacts(knowledgeBase)
                .FirstOrDefault(candidate =>
                    IsSameGroundPredicate(candidate.Predicate, query));

            return new ReasoningResult(relationalProof);
        }

        if (!TryGetFactKey(query, out var queryKey))
        {
            return new ReasoningResult(null);
        }

        var proof = DeriveUnaryFacts(knowledgeBase).FirstOrDefault(candidate =>
            TryGetFactKey(candidate.Predicate, out var key) &&
            key == queryKey);

        return new ReasoningResult(proof);
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

        var knownFacts = DeriveUnaryFacts(knowledgeBase);

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

            var supportingPremises = ImmutableArray.CreateBuilder<ProofNode>();
            supportingPremises.Add(candidate);
            var allConditionsSupported = true;

            foreach (var condition in conditions.Skip(1))
            {
                var matchingProof = knownFacts.FirstOrDefault(proof =>
                    TryGetFactKey(proof.Predicate, out var key) &&
                    string.Equals(
                        key.Predicate,
                        condition.Predicate.Value,
                        StringComparison.Ordinal) &&
                    key.Individual == candidateKey.Individual);

                if (matchingProof is null)
                {
                    allConditionsSupported = false;
                    break;
                }

                supportingPremises.Add(matchingProof);
            }

            if (!allConditionsSupported)
            {
                continue;
            }

            var additionalPremises = candidate.AdditionalPremises.IsDefault
                ? ImmutableArray<ProofNode>.Empty
                : candidate.AdditionalPremises;

            var proof = candidate with
            {
                AdditionalPremises = additionalPremises
                    .AddRange(supportingPremises.Skip(1))
            };

            return new ReasoningResult(proof);
        }

        return new ReasoningResult(null);
    }

    private static List<ProofNode> DeriveUnaryFacts(KnowledgeBase knowledgeBase)
    {
        var knownFacts = new List<ProofNode>();
        var knownKeys = new HashSet<(string Predicate, SemanticTerm Individual)>();

        foreach (var fact in knowledgeBase.Facts)
        {
            if (TryGetFactKey(fact.Predicate, out var key) &&
                knownKeys.Add(key))
            {
                knownFacts.Add(new ProofNode(
                    fact.Predicate,
                    fact.SourceText,
                    null,
                    fact.ResolvedReferent,
                    SourceKind: fact.SourceKind,
                    AdditionalSources: fact.AdditionalSources,
                    SourceId: fact.SourceId));
            }
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

                    var supportingPremises = ImmutableArray.CreateBuilder<ProofNode>();
                    supportingPremises.Add(candidatePremise);
                    var allPremisesSupported = true;

                    foreach (var requiredPremise in rule.Premises.Skip(1))
                    {
                        var matchingProof = knownFacts.FirstOrDefault(proof =>
                            TryGetFactKey(proof.Predicate, out var key) &&
                            string.Equals(
                                key.Predicate,
                                requiredPremise.Predicate.Value,
                                StringComparison.Ordinal) &&
                            key.Individual == candidateKey.Individual);

                        if (matchingProof is null)
                        {
                            allPremisesSupported = false;
                            break;
                        }

                        supportingPremises.Add(matchingProof);
                    }

                    if (!allPremisesSupported)
                    {
                        continue;
                    }

                    var conclusion = CreateGroundPredicate(
                        rule.Conclusion.Predicate,
                        candidateKey.Individual);

                    if (!TryGetFactKey(conclusion, out var conclusionKey) ||
                        !knownKeys.Add(conclusionKey))
                    {
                        continue;
                    }

                    var premises = supportingPremises.ToImmutable();
                    knownFacts.Add(new ProofNode(
                        conclusion,
                        rule.SourceText,
                        premises[0],
                        AdditionalPremises: premises.Skip(1).ToImmutableArray(),
                        SourceKind: rule.SourceKind,
                        AdditionalSources: rule.AdditionalSources,
                        Kind: ProofNodeKind.Derived,
                        SourceId: rule.SourceId));

                    added = true;
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

                var supportingPremises = ImmutableArray.CreateBuilder<ProofNode>();
                supportingPremises.Add(candidatePremise);
                var allPremisesSupported = true;

                foreach (var requiredPremise in rule.Premises.Skip(1))
                {
                    var matchingProof = unaryFacts.FirstOrDefault(proof =>
                        TryGetFactKey(proof.Predicate, out var key) &&
                        string.Equals(
                            key.Predicate,
                            requiredPremise.Predicate.Value,
                            StringComparison.Ordinal) &&
                        key.Individual == candidateKey.Individual);

                    if (matchingProof is null)
                    {
                        allPremisesSupported = false;
                        break;
                    }

                    supportingPremises.Add(matchingProof);
                }

                if (!allPremisesSupported)
                {
                    continue;
                }

                var conclusion = new PredicateApplication(
                    rule.Conclusion.Predicate,
                    ImmutableArray.Create<SemanticTerm>(
                        candidateKey.Individual,
                        objectTerm));
                var premises = supportingPremises.ToImmutable();

                relationalProofs.Add(new ProofNode(
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

        return relationalProofs;
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
