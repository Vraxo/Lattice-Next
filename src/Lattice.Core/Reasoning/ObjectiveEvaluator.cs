using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

public sealed class ObjectiveEvaluator
{
    public ObjectiveEvaluation Evaluate(ObjectiveFormula objective, KnowledgeBase knowledgeBase)
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
            .Select(condition => EvaluateCondition(condition, candidate, attributes))
            .ToImmutableArray();

        return new CandidateAssessment(candidate, conditions);
    }

    private static ObjectiveConditionAssessment EvaluateCondition(
        ObjectiveCondition condition,
        SemanticIdentifier candidate,
        IReadOnlyList<KnowledgeAttribute> attributes)
    {
        if (condition.Operator == ObjectiveConditionOperator.CanFit &&
            condition.Value is ObjectiveTemporalSequenceValue sequence)
        {
            return EvaluateTemporalSequence(condition, candidate, sequence, attributes);
        }

        if (string.IsNullOrWhiteSpace(condition.TargetAttribute))
        {
            return CreateAssessment(condition, ObjectiveConditionAssessmentStatus.Unknown, []);
        }

        var evidence = attributes
            .Where(attribute =>
                attribute.Subject == candidate &&
                string.Equals(attribute.Name.Value, condition.TargetAttribute, StringComparison.OrdinalIgnoreCase))
            .ToImmutableArray();

        if (evidence.IsDefaultOrEmpty)
        {
            return CreateAssessment(condition, ObjectiveConditionAssessmentStatus.Unknown, evidence);
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
            return CreateAssessment(condition, ObjectiveConditionAssessmentStatus.Conflicting, evidence);
        }

        return CreateAssessment(condition, Compare(condition, distinctValues[0]), evidence);
    }

    private static ObjectiveConditionAssessment EvaluateTemporalSequence(
        ObjectiveCondition condition,
        SemanticIdentifier candidate,
        ObjectiveTemporalSequenceValue sequence,
        IReadOnlyList<KnowledgeAttribute> attributes)
    {
        if (sequence.Stages.IsDefaultOrEmpty ||
            sequence.Stages.Length > 64 ||
            sequence.EarliestStart >= sequence.LatestEnd ||
            sequence.Stages.Any(stage =>
                string.IsNullOrWhiteSpace(stage.DurationAttribute) ||
                (stage.StartTimesAttribute is not null && string.IsNullOrWhiteSpace(stage.StartTimesAttribute)) ||
                (stage.AvailabilityWindowAttribute is not null && string.IsNullOrWhiteSpace(stage.AvailabilityWindowAttribute))))
        {
            return CreateAssessment(condition, ObjectiveConditionAssessmentStatus.Unknown, []);
        }

        var requiredNames = sequence.Stages
            .SelectMany(stage => new[] { stage.DurationAttribute, stage.StartTimesAttribute, stage.AvailabilityWindowAttribute })
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var evidence = attributes
            .Where(attribute =>
                attribute.Subject == candidate &&
                requiredNames.Contains(attribute.Name.Value, StringComparer.OrdinalIgnoreCase))
            .ToImmutableArray();

        var values = new Dictionary<string, ObjectiveConditionValue>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in requiredNames)
        {
            var matching = evidence
                .Where(attribute => string.Equals(attribute.Name.Value, name, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (matching.Length == 0)
            {
                return CreateAssessment(condition, ObjectiveConditionAssessmentStatus.Unknown, evidence);
            }

            var distinctValues = new List<ObjectiveConditionValue>();

            foreach (var attribute in matching)
            {
                if (!distinctValues.Any(value => AreSameValue(value, attribute.Value)))
                {
                    distinctValues.Add(attribute.Value);
                }
            }

            if (distinctValues.Count > 1)
            {
                return CreateAssessment(condition, ObjectiveConditionAssessmentStatus.Conflicting, evidence);
            }

            values.Add(name, distinctValues[0]);
        }

        foreach (var stage in sequence.Stages)
        {
            if (values[stage.DurationAttribute] is not ObjectiveDurationValue duration || duration.Minutes < 0)
            {
                return CreateAssessment(condition, ObjectiveConditionAssessmentStatus.Unknown, evidence);
            }

            if (stage.StartTimesAttribute is not null &&
                values[stage.StartTimesAttribute] is not ObjectiveTimeSetValue)
            {
                return CreateAssessment(condition, ObjectiveConditionAssessmentStatus.Unknown, evidence);
            }

            if (stage.AvailabilityWindowAttribute is not null &&
                values[stage.AvailabilityWindowAttribute] is not ObjectiveTimeWindowValue)
            {
                return CreateAssessment(condition, ObjectiveConditionAssessmentStatus.Unknown, evidence);
            }
        }

        var fits = CanFitStage(sequence, values, 0, previousEnd: null);

        return CreateAssessment(
            condition,
            fits ? ObjectiveConditionAssessmentStatus.Satisfied : ObjectiveConditionAssessmentStatus.Violated,
            evidence);
    }

    private static bool CanFitStage(
        ObjectiveTemporalSequenceValue sequence,
        IReadOnlyDictionary<string, ObjectiveConditionValue> values,
        int stageIndex,
        TimeOnly? previousEnd)
    {
        var stage = sequence.Stages[stageIndex];
        var duration = (ObjectiveDurationValue)values[stage.DurationAttribute];
        var possibleStarts = stage.StartTimesAttribute is null
            ? new[] { previousEnd ?? sequence.EarliestStart }
            : ((ObjectiveTimeSetValue)values[stage.StartTimesAttribute]).Times.Order().ToArray();

        foreach (var start in possibleStarts)
        {
            if (start < sequence.EarliestStart || (previousEnd.HasValue && start < previousEnd.Value))
            {
                continue;
            }

            var endMinute = (long)start.Hour * 60 + start.Minute + duration.Minutes;

            if (endMinute >= 24 * 60)
            {
                continue;
            }

            var end = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes((double)endMinute));

            if (stage.AvailabilityWindowAttribute is not null)
            {
                var window = (ObjectiveTimeWindowValue)values[stage.AvailabilityWindowAttribute];

                if (start < window.Start || end > window.End)
                {
                    continue;
                }
            }

            if (stageIndex == sequence.Stages.Length - 1)
            {
                if (end <= sequence.LatestEnd)
                {
                    return true;
                }

                continue;
            }

            if (CanFitStage(sequence, values, stageIndex + 1, end))
            {
                return true;
            }
        }

        return false;
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
            ObjectiveConditionOperator.AtMost => CompareMoney(condition.Value, candidateValue),
            ObjectiveConditionOperator.Excludes => CompareExclusion(condition.Value, candidateValue),
            ObjectiveConditionOperator.Equals => CompareEquality(condition.Value, candidateValue),
            ObjectiveConditionOperator.Within => CompareTimeWindow(condition.Value, candidateValue),
            ObjectiveConditionOperator.Desires => ComparePreference(condition.Value, candidateValue),
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
            ObjectiveTextValue text => string.Equals(text.Value, excluded.Value, StringComparison.OrdinalIgnoreCase),
            ObjectiveTextSetValue set => set.Values.Contains(excluded.Value, StringComparer.OrdinalIgnoreCase),
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
        if (conditionValue is ObjectiveTextValue expected && candidateValue is ObjectiveTextValue actual)
        {
            return string.Equals(expected.Value, actual.Value, StringComparison.OrdinalIgnoreCase)
                ? ObjectiveConditionAssessmentStatus.Satisfied
                : ObjectiveConditionAssessmentStatus.Violated;
        }

        if (conditionValue is ObjectiveMoneyValue expectedMoney && candidateValue is ObjectiveMoneyValue actualMoney)
        {
            if (!string.Equals(expectedMoney.CurrencySymbol, actualMoney.CurrencySymbol, StringComparison.Ordinal))
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

            var desiredValues = desiredSet.Values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
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

        if (conditionValue is ObjectiveTextValue desired && candidateValue is ObjectiveTextValue available)
        {
            return string.Equals(desired.Value, available.Value, StringComparison.OrdinalIgnoreCase)
                ? ObjectiveConditionAssessmentStatus.Satisfied
                : ObjectiveConditionAssessmentStatus.Violated;
        }

        return ObjectiveConditionAssessmentStatus.Unknown;
    }

    private static bool AreSameValue(ObjectiveConditionValue left, ObjectiveConditionValue right) =>
        (left, right) switch
        {
            (ObjectiveTextSetValue leftSet, ObjectiveTextSetValue rightSet) =>
                leftSet.Values.SequenceEqual(rightSet.Values, StringComparer.OrdinalIgnoreCase),
            (ObjectiveTimeSetValue leftSet, ObjectiveTimeSetValue rightSet) =>
                leftSet.Times.SequenceEqual(rightSet.Times),
            _ => Equals(left, right)
        };

    private static ObjectiveConditionAssessment CreateAssessment(
        ObjectiveCondition condition,
        ObjectiveConditionAssessmentStatus status,
        ImmutableArray<KnowledgeAttribute> evidence) =>
        new(condition.Name, condition.Role, status, evidence);
}
