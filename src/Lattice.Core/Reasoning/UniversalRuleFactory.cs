using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

internal static class UniversalRuleFactory
{
    internal static bool TryCreate(
        SemanticFormula meaning,
        string sourceText,
        KnowledgeSourceKind sourceKind,
        string? sourceId,
        out UniversalRule rule,
        out ImmutableArray<KnowledgeFact> descriptionFacts)
    {
        rule = null!;
        descriptionFacts = ImmutableArray<KnowledgeFact>.Empty;

        if (meaning is not UniversalQuantification universal ||
            universal.Body is not Implication implication ||
            universal.Variable is not { } variable)
        {
            return false;
        }

        if (implication.Conclusion is ExistentialQuantification existentialConclusion)
        {
            if (!TryGetBoundRulePremises(
                    implication.Premise,
                    variable,
                    out var existentialPremises,
                    out var existentialNegativePremises,
                    out var existentialRelationalPremises,
                    out var existentialNegativeRelationalPremises) ||
                !TryCreateExistentialConclusion(
                    existentialConclusion,
                    variable,
                    out var existentialRuleConclusion))
            {
                return false;
            }

            rule = new UniversalRule(
                existentialPremises,
                existentialRuleConclusion!.Relation,
                sourceText,
                sourceKind,
                SourceId: sourceId)
            {
                ConclusionPolarity = EvidencePolarity.Positive,
                NegativePremises = existentialNegativePremises,
                RelationalPremises = existentialRelationalPremises,
                NegativeRelationalPremises = existentialNegativeRelationalPremises,
                ExistentialConclusion = existentialRuleConclusion
            };

            return true;
        }

        var ruleConclusion = implication.Conclusion switch
        {
            PredicateApplication predicate => predicate,
            Negation { Formula: PredicateApplication predicate } => predicate,
            _ => null
        };
        var rulePolarity = implication.Conclusion is Negation
            ? EvidencePolarity.Negative
            : EvidencePolarity.Positive;

        if (ruleConclusion is null ||
            !TryGetBoundRulePremises(
                implication.Premise,
                variable,
                out var premises,
                out var negativePremises,
                out var relationalPremises,
                out var negativeRelationalPremises) ||
            !TryNormalizeUniversalConclusion(
                ruleConclusion,
                variable,
                out var normalizedConclusion,
                out var conclusionDescriptionFacts))
        {
            return false;
        }

        rule = new UniversalRule(
            premises,
            normalizedConclusion,
            sourceText,
            sourceKind,
            SourceId: sourceId)
        {
            ConclusionPolarity = rulePolarity,
            NegativePremises = negativePremises,
            RelationalPremises = relationalPremises,
            NegativeRelationalPremises = negativeRelationalPremises
        };

        descriptionFacts = conclusionDescriptionFacts
            .Select(predicate => new KnowledgeFact(
                predicate,
                sourceText,
                SourceKind: sourceKind,
                SourceId: sourceId))
            .ToImmutableArray();

        return true;
    }

    private static bool TryCreateExistentialConclusion(
        ExistentialQuantification existential,
        VariableTerm subject,
        out ExistentialRuleConclusion? conclusion)
    {
        conclusion = null;

        var conjuncts = existential.Body switch
        {
            PredicateApplication predicate => ImmutableArray.Create<SemanticFormula>(predicate),
            Conjunction conjunction when !conjunction.Conjuncts.IsDefaultOrEmpty =>
                conjunction.Conjuncts,
            _ => ImmutableArray<SemanticFormula>.Empty
        };

        if (conjuncts.IsDefaultOrEmpty)
        {
            return false;
        }

        PredicateApplication? relation = null;
        var conditions = ImmutableArray.CreateBuilder<PredicateApplication>();

        foreach (var conjunct in conjuncts)
        {
            if (conjunct is not PredicateApplication predicate ||
                string.IsNullOrWhiteSpace(predicate.Predicate.Value) ||
                predicate.Arguments.IsDefault)
            {
                return false;
            }

            if (predicate.Arguments.Length == 2 &&
                predicate.Arguments[0] is VariableTerm relationSubject &&
                relationSubject.Identifier == subject.Identifier &&
                predicate.Arguments[1] is VariableTerm relationWitness &&
                relationWitness.Identifier == existential.Variable.Identifier)
            {
                if (relation is not null)
                {
                    return false;
                }

                relation = predicate;
                continue;
            }

            if (predicate.Arguments.Length == 1 &&
                predicate.Arguments[0] is VariableTerm conditionWitness &&
                conditionWitness.Identifier == existential.Variable.Identifier)
            {
                conditions.Add(predicate);
                continue;
            }

            return false;
        }

        if (relation is null || conditions.Count == 0 ||
            string.IsNullOrWhiteSpace(subject.Identifier.Value) ||
            string.IsNullOrWhiteSpace(existential.Variable.Identifier.Value))
        {
            return false;
        }

        conclusion = new ExistentialRuleConclusion(
            existential.Variable,
            conditions.ToImmutable(),
            relation);

        return true;
    }

    private static bool TryGetBoundRulePremises(
        SemanticFormula formula,
        VariableTerm variable,
        out ImmutableArray<PredicateApplication> positivePremises,
        out ImmutableArray<PredicateApplication> negativePremises,
        out ImmutableArray<PredicateApplication> relationalPremises,
        out ImmutableArray<PredicateApplication> negativeRelationalPremises)
    {
        positivePremises = ImmutableArray<PredicateApplication>.Empty;
        negativePremises = ImmutableArray<PredicateApplication>.Empty;
        relationalPremises = ImmutableArray<PredicateApplication>.Empty;
        negativeRelationalPremises = ImmutableArray<PredicateApplication>.Empty;

        var conjuncts = formula switch
        {
            PredicateApplication predicate => ImmutableArray.Create<SemanticFormula>(predicate),
            Negation { Formula: PredicateApplication predicate } =>
                ImmutableArray.Create<SemanticFormula>(new Negation(predicate)),
            Conjunction conjunction when !conjunction.Conjuncts.IsDefaultOrEmpty =>
                conjunction.Conjuncts,
            _ => ImmutableArray<SemanticFormula>.Empty
        };

        if (conjuncts.IsDefaultOrEmpty ||
            (formula is Conjunction compound &&
             conjuncts.Length != compound.Conjuncts.Length))
        {
            return false;
        }

        var positiveBuilder = ImmutableArray.CreateBuilder<PredicateApplication>();
        var negativeBuilder = ImmutableArray.CreateBuilder<PredicateApplication>();
        var relationalBuilder = ImmutableArray.CreateBuilder<PredicateApplication>();
        var negativeRelationalBuilder = ImmutableArray.CreateBuilder<PredicateApplication>();

        foreach (var conjunct in conjuncts)
        {
            var isNegative = conjunct is Negation;
            var predicate = conjunct switch
            {
                PredicateApplication positive => positive,
                Negation { Formula: PredicateApplication negative } => negative,
                _ => null
            };

            if (predicate is null || string.IsNullOrWhiteSpace(predicate.Predicate.Value) || predicate.Arguments.IsDefault) return false;
            if (predicate.Arguments.Length == 1 && KnowledgeFactFactory.IsBoundUnaryPredicate(predicate, variable))
            {
                if (isNegative) negativeBuilder.Add(predicate);
                else positiveBuilder.Add(predicate);
                continue;
            }
            if (predicate.Arguments.Length == 2 &&
                predicate.Arguments[0] is VariableTerm subject && subject.Identifier == variable.Identifier &&
                predicate.Arguments[1] is IndividualTerm objectTerm && !string.IsNullOrWhiteSpace(objectTerm.Identifier.Value))
            {
                if (isNegative) negativeRelationalBuilder.Add(predicate);
                else relationalBuilder.Add(predicate);
                continue;
            }
            return false;
        }

        if (relationalBuilder.Count + negativeRelationalBuilder.Count > 1)
        {
            return false;
        }

        positivePremises = positiveBuilder.ToImmutable();
        negativePremises = negativeBuilder.ToImmutable();
        relationalPremises = relationalBuilder.ToImmutable();
        negativeRelationalPremises = negativeRelationalBuilder.ToImmutable();
        return !positivePremises.IsEmpty || !negativePremises.IsEmpty;
    }

    private static bool TryNormalizeUniversalConclusion(
        PredicateApplication conclusion,
        VariableTerm variable,
        out PredicateApplication normalizedConclusion,
        out List<PredicateApplication> descriptionFacts)
    {
        normalizedConclusion = conclusion;
        descriptionFacts = [];

        if (KnowledgeFactFactory.IsBoundUnaryPredicate(conclusion, variable))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(conclusion.Predicate.Value) ||
            string.IsNullOrWhiteSpace(variable.Identifier.Value) ||
            conclusion.Arguments.IsDefault ||
            conclusion.Arguments.Length != 2 ||
            conclusion.Arguments[0] is not VariableTerm subject ||
            subject.Identifier != variable.Identifier)
        {
            return false;
        }

        SemanticTerm objectTerm;

        switch (conclusion.Arguments[1])
        {
            case IndividualTerm individual when
                !string.IsNullOrWhiteSpace(individual.Identifier.Value):
                objectTerm = individual;
                break;

            case DescriptiveIndividualTerm description
                when KnowledgeFactFactory.IsValidDescription(description):
                objectTerm = new IndividualTerm(description.Identifier);
                descriptionFacts.Add(KnowledgeFactFactory.CreateUnaryFact(
                    description.Head,
                    description.Identifier));

                foreach (var modifier in description.Modifiers)
                {
                    descriptionFacts.Add(KnowledgeFactFactory.CreateUnaryFact(
                        modifier,
                        description.Identifier));
                }

                break;

            default:
                return false;
        }

        normalizedConclusion = conclusion with
        {
            Arguments = ImmutableArray.Create<SemanticTerm>(variable, objectTerm)
        };

        return true;
    }
}
