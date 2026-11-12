using System.Collections.Immutable;
using Lattice.Core.Language;
using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

public sealed class KnowledgeBase
{
    private readonly List<KnowledgeFact> facts = [];
    private readonly List<UniversalRule> rules = [];
    private readonly List<KnowledgeAttribute> attributes = [];
    private readonly IReadOnlyList<KnowledgeFact> readOnlyFacts;
    private readonly IReadOnlyList<UniversalRule> readOnlyRules;
    private readonly IReadOnlyList<KnowledgeAttribute> readOnlyAttributes;
    private long nextExistentialWitnessId;

    public KnowledgeBase()
    {
        readOnlyFacts = facts.AsReadOnly();
        readOnlyRules = rules.AsReadOnly();
        readOnlyAttributes = attributes.AsReadOnly();
    }

    public IReadOnlyList<KnowledgeFact> Facts => readOnlyFacts;

    public IReadOnlyList<UniversalRule> Rules => readOnlyRules;

    public IReadOnlyList<KnowledgeAttribute> Attributes => readOnlyAttributes;

    public bool TryAddAttribute(KnowledgeAttribute attribute, out string? error)
    {
        ArgumentNullException.ThrowIfNull(attribute);

        error = null;

        if (string.IsNullOrWhiteSpace(attribute.Subject.Value) ||
            string.IsNullOrWhiteSpace(attribute.Name.Value) ||
            !IsValidAttributeValue(attribute.Value) ||
            string.IsNullOrWhiteSpace(attribute.SourceText))
        {
            error = "A knowledge attribute requires a subject, name, valid typed value, and source text.";
            return false;
        }

        var existingIndex = attributes.FindIndex(existing =>
            existing.Subject == attribute.Subject &&
            existing.Name == attribute.Name &&
            AreSameAttributeValue(existing.Value, attribute.Value));

        if (existingIndex < 0)
        {
            attributes.Add(attribute);
            return true;
        }

        var existingAttribute = attributes[existingIndex];
        var sources = existingAttribute.Sources
            .AddRange(attribute.Sources)
            .Distinct()
            .ToImmutableArray();

        attributes[existingIndex] = existingAttribute with
        {
            AdditionalSources = sources.Skip(1).ToImmutableArray(),
            IncludeInSummary = existingAttribute.IncludeInSummary || attribute.IncludeInSummary
        };
        return true;
    }

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
            KnowledgeFactFactory.TryGetBoundUnaryPredicates(
                existential.Body,
                existential.Variable,
                out var existentialConditions))
        {
            var witness = new AnonymousIndividualTerm(
                new SemanticIdentifier($"witness-{++nextExistentialWitnessId}"));

            foreach (var fact in KnowledgeFactFactory.CreateExistentialFacts(
                existentialConditions,
                witness,
                utterance.SourceText,
                sourceKind,
                sourceId))
            {
                AddFact(fact);
            }

            kind = KnowledgeEntryKind.Fact;
            return true;
        }

        if (KnowledgeFactFactory.TryCreateGroundFacts(
            utterance.Meaning,
            utterance.SourceText,
            utterance.ResolvedReferent,
            sourceKind,
            sourceId,
            out var groundFacts))
        {
            foreach (var fact in groundFacts)
            {
                AddFact(fact);
            }

            kind = KnowledgeEntryKind.Fact;
            return true;
        }

        if (UniversalRuleFactory.TryCreate(
            utterance.Meaning,
            utterance.SourceText,
            sourceKind,
            sourceId,
            out var candidateRule,
            out var conclusionDescriptionFacts))
        {
            var existingRuleIndex = rules.FindIndex(rule =>
                rule.ConclusionPolarity == candidateRule.ConclusionPolarity &&
                AreSamePremises(rule.Premises, candidateRule.Premises) &&
                AreSamePremises(rule.NegativePremises, candidateRule.NegativePremises) &&
                AreSameRelationalPremises(rule.RelationalPremises, candidateRule.RelationalPremises) &&
                AreSameRelationalPremises(rule.NegativeRelationalPremises, candidateRule.NegativeRelationalPremises) &&
                IsSamePredicate(rule.Conclusion, candidateRule.Conclusion));

            if (existingRuleIndex < 0)
            {
                rules.Add(candidateRule);
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

            foreach (var fact in conclusionDescriptionFacts)
            {
                AddFact(fact);
            }

            kind = KnowledgeEntryKind.Rule;
            return true;
        }

        error =
            "Only ground facts, existential assertions with unary conditions, and universal rules with one or more unary premises, optionally one positive or explicitly negative fixed-object relational premise, and either one unary conclusion or one binary relation to a concrete individual are supported by the current reasoning engine.";
        return false;
    }

    private static bool IsValidAttributeValue(ObjectiveConditionValue value) =>
        value switch
        {
            ObjectiveMoneyValue money =>
                money.Amount >= 0m && !string.IsNullOrWhiteSpace(money.CurrencySymbol),
            ObjectiveTextValue text =>
                !string.IsNullOrWhiteSpace(text.Value),
            ObjectiveTextSetValue set =>
                (!set.Values.IsDefaultOrEmpty || !set.ExcludedValues.IsDefaultOrEmpty) &&
                set.Values.All(item => !string.IsNullOrWhiteSpace(item)) &&
                set.ExcludedValues.All(item => !string.IsNullOrWhiteSpace(item)),
            ObjectiveTimeWindowValue time =>
                time.Start < time.End,
            ObjectiveDurationValue duration =>
                duration.Minutes >= 0,
            ObjectiveTimeSetValue times =>
                !times.Times.IsDefaultOrEmpty &&
                times.Times.Distinct().Count() == times.Times.Length,
            ObjectiveTemporalPlanValue plan =>
                !plan.Stages.IsDefaultOrEmpty &&
                plan.Stages.Length <= 64 &&
                plan.Stages.All(stage =>
                    stage is not null &&
                    !string.IsNullOrWhiteSpace(stage.DurationAttribute) &&
                    (stage.StartTimesAttribute is null || !string.IsNullOrWhiteSpace(stage.StartTimesAttribute)) &&
                    (stage.AvailabilityWindowAttribute is null || !string.IsNullOrWhiteSpace(stage.AvailabilityWindowAttribute))),
            ObjectiveSumValue sum =>
                sum.Attributes.Length is >= 2 and <= 64 &&
                sum.Attributes.All(name => !string.IsNullOrWhiteSpace(name)) &&
                sum.Attributes.Distinct(StringComparer.OrdinalIgnoreCase).Count() == sum.Attributes.Length,
            _ => false
        };

    private static bool AreSameAttributeValue(
        ObjectiveConditionValue left,
        ObjectiveConditionValue right) =>
        (left, right) switch
        {
            (ObjectiveTextSetValue leftSet, ObjectiveTextSetValue rightSet) =>
                leftSet.Values.SequenceEqual(rightSet.Values, StringComparer.OrdinalIgnoreCase) &&
                leftSet.ExcludedValues.SequenceEqual(rightSet.ExcludedValues, StringComparer.OrdinalIgnoreCase),
            (ObjectiveTimeSetValue leftSet, ObjectiveTimeSetValue rightSet) =>
                leftSet.Times.SequenceEqual(rightSet.Times),
            (ObjectiveTemporalPlanValue leftPlan, ObjectiveTemporalPlanValue rightPlan) =>
                leftPlan.Stages.SequenceEqual(rightPlan.Stages),
            (ObjectiveSumValue leftSum, ObjectiveSumValue rightSum) =>
                leftSum.Attributes.SequenceEqual(rightSum.Attributes, StringComparer.OrdinalIgnoreCase),
            _ => Equals(left, right)
        };

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

    private static bool AreSamePremises(
        ImmutableArray<PredicateApplication> left,
        ImmutableArray<PredicateApplication> right) =>
        !left.IsDefault &&
        !right.IsDefault &&
        left.Length == right.Length &&
        left.Zip(right).All(pair => IsSamePredicate(pair.First, pair.Second));


    private static bool AreSameRelationalPremises(
        ImmutableArray<PredicateApplication> left,
        ImmutableArray<PredicateApplication> right)
    {
        if (left.IsDefault || right.IsDefault || left.Length != right.Length)
        {
            return false;
        }

        for (var index = 0; index < left.Length; index++)
        {
            var leftPremise = left[index];
            var rightPremise = right[index];

            if (leftPremise.Predicate != rightPremise.Predicate ||
                leftPremise.Arguments.IsDefault ||
                rightPremise.Arguments.IsDefault ||
                leftPremise.Arguments.Length != 2 ||
                rightPremise.Arguments.Length != 2 ||
                leftPremise.Arguments[0] is not VariableTerm leftSubject ||
                rightPremise.Arguments[0] is not VariableTerm rightSubject ||
                !string.Equals(
                    leftSubject.Identifier.Value,
                    rightSubject.Identifier.Value,
                    StringComparison.Ordinal) ||
                leftPremise.Arguments[1] is not IndividualTerm leftObject ||
                rightPremise.Arguments[1] is not IndividualTerm rightObject ||
                !string.Equals(
                    leftObject.Identifier.Value,
                    rightObject.Identifier.Value,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSamePredicate(
        PredicateApplication left,
        PredicateApplication right) =>
        left.Predicate == right.Predicate &&
        !left.Arguments.IsDefault &&
        !right.Arguments.IsDefault &&
        left.Arguments.SequenceEqual(right.Arguments);


    private static bool IsSameFact(
        PredicateApplication left,
        PredicateApplication right) =>
        left.Predicate == right.Predicate &&
        !left.Arguments.IsDefault &&
        !right.Arguments.IsDefault &&
        left.Arguments.SequenceEqual(right.Arguments);
}
