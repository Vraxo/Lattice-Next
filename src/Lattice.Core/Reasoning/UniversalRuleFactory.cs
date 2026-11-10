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
                out var relationalPremises) ||
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
            RelationalPremises = relationalPremises
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

    private static bool TryGetBoundRulePremises(
        SemanticFormula formula,
        VariableTerm variable,
        out ImmutableArray<PredicateApplication> positivePremises,
        out ImmutableArray<PredicateApplication> negativePremises,
        out ImmutableArray<PredicateApplication> relationalPremises)
    {
        positivePremises = ImmutableArray<PredicateApplication>.Empty;
        negativePremises = ImmutableArray<PredicateApplication>.Empty;
        relationalPremises = ImmutableArray<PredicateApplication>.Empty;

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
            if (!isNegative && predicate.Arguments.Length == 2 &&
                predicate.Arguments[0] is VariableTerm subject && subject.Identifier == variable.Identifier &&
                predicate.Arguments[1] is IndividualTerm objectTerm && !string.IsNullOrWhiteSpace(objectTerm.Identifier.Value))
            {
                relationalBuilder.Add(predicate);
                continue;
            }
            return false;
        }

        if (relationalBuilder.Count > 1)
        {
            return false;
        }

        positivePremises = positiveBuilder.ToImmutable();
        negativePremises = negativeBuilder.ToImmutable();
        relationalPremises = relationalBuilder.ToImmutable();
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
