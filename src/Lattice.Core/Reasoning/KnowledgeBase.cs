using System.Collections.Immutable;
using Lattice.Core.Language;
using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

public sealed class KnowledgeBase
{
    private readonly List<KnowledgeFact> facts = [];
    private readonly List<UniversalRule> rules = [];
    private readonly IReadOnlyList<KnowledgeFact> readOnlyFacts;
    private readonly IReadOnlyList<UniversalRule> readOnlyRules;
    private long nextExistentialWitnessId;

    public KnowledgeBase()
    {
        readOnlyFacts = facts.AsReadOnly();
        readOnlyRules = rules.AsReadOnly();
    }

    public IReadOnlyList<KnowledgeFact> Facts => readOnlyFacts;

    public IReadOnlyList<UniversalRule> Rules => readOnlyRules;

    public bool TryAdd(
        SemanticUtterance utterance,
        out KnowledgeEntryKind kind,
        out string? error,
        KnowledgeSourceKind sourceKind = KnowledgeSourceKind.UserAssertion,
        string? sourceId = null)
    {
        ArgumentNullException.ThrowIfNull(utterance);

        kind = default;
        error = null;

        if (utterance.Kind != UtteranceKind.Assertion)
        {
            error = "Questions cannot be added to the knowledge base.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(utterance.SourceText))
        {
            error = "The source sentence is missing.";
            return false;
        }

        if (utterance.Meaning is ExistentialQuantification existential &&
            TryGetBoundUnaryPredicates(
                existential.Body,
                existential.Variable,
                out var existentialConditions))
        {
            var witness = new AnonymousIndividualTerm(
                new SemanticIdentifier($"witness-{++nextExistentialWitnessId}"));

            foreach (var condition in existentialConditions)
            {
                AddFact(new KnowledgeFact(
                    condition with
                    {
                        Arguments = ImmutableArray.Create<SemanticTerm>(witness)
                    },
                    utterance.SourceText,
                    SourceKind: sourceKind,
                    SourceId: sourceId));
            }

            kind = KnowledgeEntryKind.Fact;
            return true;
        }

        var factMeaning = utterance.Meaning switch
        {
            PredicateApplication positivePredicate => positivePredicate,
            Negation { Formula: PredicateApplication negativePredicate } => negativePredicate,
            _ => null
        };
        var polarity = utterance.Meaning is Negation
            ? EvidencePolarity.Negative
            : EvidencePolarity.Positive;

        if (factMeaning is not null &&
            (polarity == EvidencePolarity.Positive ||
             (!factMeaning.Arguments.IsDefault && factMeaning.Arguments.Length is 1 or 2)) &&
            TryGroundPredicate(factMeaning, out var groundedPredicate, out var descriptionFacts))
        {
            AddFact(new KnowledgeFact(
                groundedPredicate,
                utterance.SourceText,
                utterance.ResolvedReferent,
                sourceKind,
                SourceId: sourceId)
            {
                Polarity = polarity
            });

            foreach (var descriptionFact in descriptionFacts)
            {
                AddFact(new KnowledgeFact(
                    descriptionFact,
                    utterance.SourceText,
                    utterance.ResolvedReferent,
                    sourceKind,
                    SourceId: sourceId));
            }

            kind = KnowledgeEntryKind.Fact;
            return true;
        }

        var ruleConclusion = utterance.Meaning is UniversalQuantification quantified &&
            quantified.Body is Implication ruleImplication
                ? ruleImplication.Conclusion switch
                {
                    PredicateApplication predicate => predicate,
                    Negation { Formula: PredicateApplication predicate } => predicate,
                    _ => null
                }
                : null;
        var rulePolarity = utterance.Meaning is UniversalQuantification negativeQuantified &&
            negativeQuantified.Body is Implication negativeImplication &&
            negativeImplication.Conclusion is Negation
                ? EvidencePolarity.Negative
                : EvidencePolarity.Positive;

        if (utterance.Meaning is UniversalQuantification universal &&
            universal.Body is Implication implication &&
            universal.Variable is { } variable &&
            TryGetBoundUnaryPredicates(implication.Premise, variable, out var premises) &&
            ruleConclusion is not null &&
            TryNormalizeUniversalConclusion(
                ruleConclusion,
                variable,
                out var normalizedConclusion,
                out var conclusionDescriptionFacts))
        {
            var existingRuleIndex = rules.FindIndex(rule =>
                rule.ConclusionPolarity == rulePolarity &&
                AreSamePremises(rule.Premises, premises) &&
                IsSamePredicate(rule.Conclusion, normalizedConclusion));

            if (existingRuleIndex < 0)
            {
                rules.Add(new UniversalRule(
                    premises,
                    normalizedConclusion,
                    utterance.SourceText,
                    sourceKind,
                    SourceId: sourceId)
                {
                    ConclusionPolarity = rulePolarity
                });
            }
            else
            {
                var existingRule = rules[existingRuleIndex];
                var sources = existingRule.Sources
                    .Add(new KnowledgeSource(sourceKind, utterance.SourceText, SourceId: sourceId))
                    .Distinct()
                    .ToImmutableArray();
                rules[existingRuleIndex] = existingRule with
                {
                    AdditionalSources = sources.Skip(1).ToImmutableArray()
                };
            }

            foreach (var descriptionFact in conclusionDescriptionFacts)
            {
                AddFact(new KnowledgeFact(
                    descriptionFact,
                    utterance.SourceText,
                    SourceKind: sourceKind,
                    SourceId: sourceId));
            }

            kind = KnowledgeEntryKind.Rule;
            return true;
        }

        error =
            "Only ground facts, existential assertions with unary conditions, and universal rules with one or more unary premises and either one unary conclusion or one binary relation to a concrete individual are supported by the current reasoning engine.";
        return false;
    }

    private void AddFact(KnowledgeFact fact)
    {
        var existingFactIndex = facts.FindIndex(existing =>
            existing.Polarity == fact.Polarity &&
            IsSameFact(existing.Predicate, fact.Predicate));

        if (existingFactIndex < 0)
        {
            facts.Add(fact);
            return;
        }

        var existingFact = facts[existingFactIndex];
        var sources = existingFact.Sources
            .AddRange(fact.Sources)
            .Distinct()
            .ToImmutableArray();

        facts[existingFactIndex] = existingFact with
        {
            AdditionalSources = sources.Skip(1).ToImmutableArray()
        };
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

    private static bool IsValidDescription(DescriptiveIndividualTerm description) =>
        !string.IsNullOrWhiteSpace(description.Head.Value) &&
        !description.Modifiers.IsDefault &&
        description.Modifiers.All(modifier =>
            !string.IsNullOrWhiteSpace(modifier.Value));

    private static PredicateApplication CreateUnaryFact(
        SemanticIdentifier predicate,
        SemanticIdentifier individual) =>
        new(
            predicate,
            ImmutableArray.Create<SemanticTerm>(new IndividualTerm(individual)));

    private static bool TryGetBoundUnaryPredicates(
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

    private static bool AreSamePremises(
        ImmutableArray<PredicateApplication> left,
        ImmutableArray<PredicateApplication> right) =>
        !left.IsDefault &&
        !right.IsDefault &&
        left.Length == right.Length &&
        left.Zip(right).All(pair => IsSamePredicate(pair.First, pair.Second));

    private static bool IsSamePredicate(
        PredicateApplication left,
        PredicateApplication right) =>
        left.Predicate == right.Predicate &&
        !left.Arguments.IsDefault &&
        !right.Arguments.IsDefault &&
        left.Arguments.SequenceEqual(right.Arguments);

    private static bool IsBoundUnaryPredicate(
        PredicateApplication predicate,
        VariableTerm variable) =>
        !string.IsNullOrWhiteSpace(predicate.Predicate.Value) &&
        !string.IsNullOrWhiteSpace(variable.Identifier.Value) &&
        !predicate.Arguments.IsDefault &&
        predicate.Arguments.Length == 1 &&
        predicate.Arguments[0] is VariableTerm argument &&
        argument.Identifier == variable.Identifier;

    private static bool TryNormalizeUniversalConclusion(
        PredicateApplication conclusion,
        VariableTerm variable,
        out PredicateApplication normalizedConclusion,
        out List<PredicateApplication> descriptionFacts)
    {
        normalizedConclusion = conclusion;
        descriptionFacts = [];

        if (IsBoundUnaryPredicate(conclusion, variable))
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
                when IsValidDescription(description):
                objectTerm = new IndividualTerm(description.Identifier);
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

        normalizedConclusion = conclusion with
        {
            Arguments = ImmutableArray.Create<SemanticTerm>(variable, objectTerm)
        };

        return true;
    }

    private static bool IsSameFact(
        PredicateApplication left,
        PredicateApplication right) =>
        left.Predicate == right.Predicate &&
        !left.Arguments.IsDefault &&
        !right.Arguments.IsDefault &&
        left.Arguments.SequenceEqual(right.Arguments);
}
