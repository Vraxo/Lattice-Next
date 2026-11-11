using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

public sealed class ObjectiveEvaluator
{
    public ObjectiveEvaluation Evaluate(
        ObjectiveFormula objective,
        KnowledgeBase knowledgeBase)
    {
        ArgumentNullException.ThrowIfNull(objective);
        ArgumentNullException.ThrowIfNull(knowledgeBase);

        var candidates = knowledgeBase.Attributes
            .Select(attribute => attribute.Subject)
            .Distinct()
            .OrderBy(candidate => candidate.Value, StringComparer.Ordinal)
            .Select(candidate => EvaluateCandidate(objective, candidate, knowledgeBase.Attributes))
            .ToImmutableArray();

        return new ObjectiveEvaluation(candidates);
    }

    private static CandidateAssessment EvaluateCandidate(
        ObjectiveFormula objective,
        SemanticIdentifier candidate,
        IReadOnlyList<KnowledgeAttribute> attributes)
    {
        var conditions = objective.Conditions
            .Select(condition => EvaluateCondition(
                condition,
                candidate,
                attributes))
            .ToImmutableArray();

        return new CandidateAssessment(candidate, conditions);
    }

    private static ObjectiveConditionAssessment EvaluateCondition(
        ObjectiveCondition condition,
        SemanticIdentifier candidate,
        IReadOnlyList<KnowledgeAttribute> attributes)
    {
        if (string.IsNullOrWhiteSpace(condition.TargetAttribute))
        {
            return CreateAssessment(
                condition,
                ObjectiveConditionAssessmentStatus.Unknown,
                ImmutableArray<KnowledgeAttribute>.Empty);
        }

        var evidence = attributes
            .Where(attribute =>
                attribute.Subject == candidate &&
                string.Equals(
                    attribute.Name.Value,
                    condition.TargetAttribute,
                    StringComparison.OrdinalIgnoreCase))
            .ToImmutableArray();

        if (evidence.IsDefaultOrEmpty)
        {
            return CreateAssessment(
                condition,
                ObjectiveConditionAssessmentStatus.Unknown,
                evidence);
        }

        var distinctValues = new List<ObjectiveConditionValue>();

        foreach (var attribute in evidence)
        {
            if (!distinctValues.Any(value => AreSameValue(value, attribute.Value)))
            {
                distinctValues.Add(attribute.Value);
            }
        }

        if (distinctValues.Count > 1)
        {
            return CreateAssessment(
                condition,
                ObjectiveConditionAssessmentStatus.Conflicting,
                evidence);
        }

        return CreateAssessment(
            condition,
            Compare(condition, distinctValues[0]),
            evidence);
    }

    private static ObjectiveConditionAssessmentStatus Compare(
        ObjectiveCondition condition,
        ObjectiveConditionValue candidateValue)
    {
        if (condition.Value is null)
        {
            return ObjectiveConditionAssessmentStatus.Unknown;
        }

        return condition.Operator switch
        {
            ObjectiveConditionOperator.AtMost =>
                CompareMoney(condition.Value, candidateValue),
            ObjectiveConditionOperator.Excludes =>
                CompareExclusion(condition.Value, candidateValue),
            ObjectiveConditionOperator.Equals =>
                CompareEquality(condition.Value, candidateValue),
            ObjectiveConditionOperator.Within =>
                CompareTimeWindow(condition.Value, candidateValue),
            ObjectiveConditionOperator.Desires =>
                ComparePreference(condition.Value, candidateValue),
            _ => ObjectiveConditionAssessmentStatus.Unknown
        };
    }

    private static ObjectiveConditionAssessmentStatus CompareMoney(
        ObjectiveConditionValue conditionValue,
        ObjectiveConditionValue candidateValue)
    {
        if (conditionValue is not ObjectiveMoneyValue limit ||
            candidateValue is not ObjectiveMoneyValue cost ||
            !string.Equals(limit.CurrencySymbol, cost.CurrencySymbol, StringComparison.Ordinal))
        {
            return ObjectiveConditionAssessmentStatus.Unknown;
        }

        return cost.Amount <= limit.Amount
            ? ObjectiveConditionAssessmentStatus.Satisfied
            : ObjectiveConditionAssessmentStatus.Violated;
    }

    private static ObjectiveConditionAssessmentStatus CompareExclusion(
        ObjectiveConditionValue conditionValue,
        ObjectiveConditionValue candidateValue)
    {
        if (conditionValue is not ObjectiveTextValue excluded)
        {
            return ObjectiveConditionAssessmentStatus.Unknown;
        }

        var containsExcluded = candidateValue switch
        {
            ObjectiveTextValue text =>
                string.Equals(text.Value, excluded.Value, StringComparison.OrdinalIgnoreCase),
            ObjectiveTextSetValue set =>
                set.Values.Contains(excluded.Value, StringComparer.OrdinalIgnoreCase),
            _ => (bool?)null
        };

        return containsExcluded switch
        {
            true => ObjectiveConditionAssessmentStatus.Violated,
            false => ObjectiveConditionAssessmentStatus.Satisfied,
            null => ObjectiveConditionAssessmentStatus.Unknown
        };
    }

    private static ObjectiveConditionAssessmentStatus CompareEquality(
        ObjectiveConditionValue conditionValue,
        ObjectiveConditionValue candidateValue)
    {
        if (conditionValue is ObjectiveTextValue expected &&
            candidateValue is ObjectiveTextValue actual)
        {
            return string.Equals(
                expected.Value,
                actual.Value,
                StringComparison.OrdinalIgnoreCase)
                ? ObjectiveConditionAssessmentStatus.Satisfied
                : ObjectiveConditionAssessmentStatus.Violated;
        }

        if (conditionValue is ObjectiveMoneyValue expectedMoney &&
            candidateValue is ObjectiveMoneyValue actualMoney)
        {
            if (!string.Equals(
                    expectedMoney.CurrencySymbol,
                    actualMoney.CurrencySymbol,
                    StringComparison.Ordinal))
            {
                return ObjectiveConditionAssessmentStatus.Unknown;
            }

            return expectedMoney.Amount == actualMoney.Amount
                ? ObjectiveConditionAssessmentStatus.Satisfied
                : ObjectiveConditionAssessmentStatus.Violated;
        }

        return ObjectiveConditionAssessmentStatus.Unknown;
    }

    private static ObjectiveConditionAssessmentStatus CompareTimeWindow(
        ObjectiveConditionValue conditionValue,
        ObjectiveConditionValue candidateValue)
    {
        if (conditionValue is not ObjectiveTimeWindowValue requested ||
            candidateValue is not ObjectiveTimeWindowValue available)
        {
            return ObjectiveConditionAssessmentStatus.Unknown;
        }

        return available.Start <= requested.Start && available.End >= requested.End
            ? ObjectiveConditionAssessmentStatus.Satisfied
            : ObjectiveConditionAssessmentStatus.Violated;
    }

    private static ObjectiveConditionAssessmentStatus ComparePreference(
        ObjectiveConditionValue conditionValue,
        ObjectiveConditionValue candidateValue)
    {
        if (conditionValue is ObjectiveTextSetValue desiredSet)
        {
            if (desiredSet.Values.IsDefaultOrEmpty)
            {
                return ObjectiveConditionAssessmentStatus.Unknown;
            }

            var availableValues = candidateValue switch
            {
                ObjectiveTextSetValue availableSet => availableSet.Values,
                ObjectiveTextValue availableText => ImmutableArray.Create(availableText.Value),
                _ => ImmutableArray<string>.Empty
            };

            if (availableValues.IsDefaultOrEmpty)
            {
                return ObjectiveConditionAssessmentStatus.Unknown;
            }

            var desiredValues = desiredSet.Values
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var matchedCount = desiredValues.Count(desired => availableValues.Contains(
                desired,
                StringComparer.OrdinalIgnoreCase));

            if (matchedCount == desiredValues.Length)
            {
                return ObjectiveConditionAssessmentStatus.Satisfied;
            }

            return matchedCount == 0
                ? ObjectiveConditionAssessmentStatus.Violated
                : ObjectiveConditionAssessmentStatus.PartiallySatisfied;
        }

        if (conditionValue is ObjectiveTextValue desired &&
            candidateValue is ObjectiveTextValue available)
        {
            return string.Equals(
                desired.Value,
                available.Value,
                StringComparison.OrdinalIgnoreCase)
                ? ObjectiveConditionAssessmentStatus.Satisfied
                : ObjectiveConditionAssessmentStatus.Violated;
        }

        return ObjectiveConditionAssessmentStatus.Unknown;
    }

    private static bool AreSameValue(
        ObjectiveConditionValue left,
        ObjectiveConditionValue right) =>
        (left, right) switch
        {
            (ObjectiveTextSetValue leftSet, ObjectiveTextSetValue rightSet) =>
                leftSet.Values.SequenceEqual(rightSet.Values, StringComparer.OrdinalIgnoreCase),
            _ => Equals(left, right)
        };

    private static ObjectiveConditionAssessment CreateAssessment(
        ObjectiveCondition condition,
        ObjectiveConditionAssessmentStatus status,
        ImmutableArray<KnowledgeAttribute> evidence) =>
        new(condition.Name, condition.Role, status, evidence);
}
