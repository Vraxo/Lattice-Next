using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

internal static class KnowledgeFactFactory
{
    internal static bool TryCreateGroundFacts(
        SemanticFormula meaning,
        string sourceText,
        SemanticIdentifier? resolvedReferent,
        KnowledgeSourceKind sourceKind,
        string? sourceId,
        out ImmutableArray<KnowledgeFact> facts)
    {
        facts = ImmutableArray<KnowledgeFact>.Empty;

        if (meaning is Conjunction conjunction)
        {
            if (conjunction.Conjuncts.IsDefaultOrEmpty)
            {
                return false;
            }

            var conjunctiveFacts = ImmutableArray.CreateBuilder<KnowledgeFact>();

            foreach (var conjunct in conjunction.Conjuncts)
            {
                if (conjunct is not PredicateApplication predicate ||
                    !TryGroundPredicate(predicate, out var groundedPredicate, out var descriptionFacts))
                {
                    return false;
                }

                conjunctiveFacts.Add(new KnowledgeFact(
                    groundedPredicate,
                    sourceText,
                    resolvedReferent,
                    sourceKind,
                    SourceId: sourceId));

                foreach (var descriptionFact in descriptionFacts)
                {
                    conjunctiveFacts.Add(new KnowledgeFact(
                        descriptionFact,
                        sourceText,
                        resolvedReferent,
                        sourceKind,
                        SourceId: sourceId));
                }
            }

            facts = conjunctiveFacts.ToImmutable();
            return true;
        }

        var factMeaning = meaning switch
        {
            PredicateApplication positivePredicate => positivePredicate,
            Negation { Formula: PredicateApplication negativePredicate } => negativePredicate,
            _ => null
        };
        var polarity = meaning is Negation
            ? EvidencePolarity.Negative
            : EvidencePolarity.Positive;

        if (factMeaning is null ||
            (polarity == EvidencePolarity.Negative &&
             (factMeaning.Arguments.IsDefault || factMeaning.Arguments.Length is not (1 or 2))) ||
            !TryGroundPredicate(factMeaning, out var groundedPredicate, out var descriptionFacts))
        {
            return false;
        }

        var builder = ImmutableArray.CreateBuilder<KnowledgeFact>(1 + descriptionFacts.Count);
        builder.Add(new KnowledgeFact(
            groundedPredicate,
            sourceText,
            resolvedReferent,
            sourceKind,
            SourceId: sourceId)
        {
            Polarity = polarity
        });

        foreach (var descriptionFact in descriptionFacts)
        {
            builder.Add(new KnowledgeFact(
                descriptionFact,
                sourceText,
                resolvedReferent,
                sourceKind,
                SourceId: sourceId));
        }

        facts = builder.ToImmutable();
        return true;
    }

    internal static ImmutableArray<KnowledgeFact> CreateExistentialFacts(
        ImmutableArray<PredicateApplication> conditions,
        SemanticTerm witness,
        string sourceText,
        KnowledgeSourceKind sourceKind,
        string? sourceId)
    {
        var facts = ImmutableArray.CreateBuilder<KnowledgeFact>(conditions.Length);

        foreach (var condition in conditions)
        {
            facts.Add(new KnowledgeFact(
                condition with
                {
                    Arguments = ImmutableArray.Create(witness)
                },
                sourceText,
                SourceKind: sourceKind,
                SourceId: sourceId));
        }

        return facts.ToImmutable();
    }

    internal static bool TryGetBoundUnaryPredicates(
        SemanticFormula formula,
        VariableTerm variable,
        out ImmutableArray<PredicateApplication> premises)
    {
        premises = formula switch
        {
            PredicateApplication predicate => ImmutableArray.Create(predicate),
            Conjunction conjunction when !conjunction.Conjuncts.IsDefaultOrEmpty =>
                conjunction.Conjuncts
                    .OfType<PredicateApplication>()
                    .ToImmutableArray(),
            _ => ImmutableArray<PredicateApplication>.Empty
        };

        if (premises.IsDefaultOrEmpty ||
            (formula is Conjunction compound &&
             premises.Length != compound.Conjuncts.Length))
        {
            return false;
        }

        return premises.All(premise => IsBoundUnaryPredicate(premise, variable));
    }


    private static bool TryGroundPredicate(
        PredicateApplication predicate,
        out PredicateApplication groundedPredicate,
        out List<PredicateApplication> descriptionFacts)
    {
        groundedPredicate = predicate;
        descriptionFacts = [];

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
                    when IsValidDescription(description):
                    arguments.Add(new IndividualTerm(description.Identifier));

                    descriptionFacts.Add(CreateUnaryFact(
                        description.Head,
                        description.Identifier));

                    foreach (var modifier in description.Modifiers)
                    {
                        descriptionFacts.Add(CreateUnaryFact(
                            modifier,
                            description.Identifier));
                    }

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

    internal static bool IsValidDescription(DescriptiveIndividualTerm description) =>
        !string.IsNullOrWhiteSpace(description.Head.Value) &&
        !description.Modifiers.IsDefault &&
        description.Modifiers.All(modifier =>
            !string.IsNullOrWhiteSpace(modifier.Value));

    internal static PredicateApplication CreateUnaryFact(
        SemanticIdentifier predicate,
        SemanticIdentifier individual) =>
        new(
            predicate,
            ImmutableArray.Create<SemanticTerm>(new IndividualTerm(individual)));


    internal static bool IsBoundUnaryPredicate(
        PredicateApplication predicate,
        VariableTerm variable) =>
        !string.IsNullOrWhiteSpace(predicate.Predicate.Value) &&
        !string.IsNullOrWhiteSpace(variable.Identifier.Value) &&
        !predicate.Arguments.IsDefault &&
        predicate.Arguments.Length == 1 &&
        predicate.Arguments[0] is VariableTerm argument &&
        argument.Identifier == variable.Identifier;


}
