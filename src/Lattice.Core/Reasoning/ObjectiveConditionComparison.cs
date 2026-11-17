using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

internal static class ObjectiveConditionComparison
{
        private static ObjectiveConditionAssessment EvaluatePreferences(
            ObjectiveCondition condition,
            ObjectiveTextSetValue desiredPreferences,
            (
                ObjectiveConditionAssessmentStatus Status,
                ObjectiveConditionValue? Value,
                ImmutableArray<KnowledgeAttribute> Evidence) resolution)
        {
            var preferences = desiredPreferences.Values
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(preference =>
                {
                    var status = CompareIndividualPreference(preference, resolution);
                    var evidence = status is ObjectiveConditionAssessmentStatus.Satisfied or
                        ObjectiveConditionAssessmentStatus.Violated or
                        ObjectiveConditionAssessmentStatus.Conflicting or
                        ObjectiveConditionAssessmentStatus.Unverified or
                        ObjectiveConditionAssessmentStatus.Stale
                        ? resolution.Evidence
                        : ImmutableArray<KnowledgeAttribute>.Empty;
    
                    return new ObjectivePreferenceAssessment(preference, status, evidence);
                })
                .ToImmutableArray();
    
            var statuses = preferences.Select(preference => preference.Status).ToArray();
            var status = statuses.Length == 0
                ? ObjectiveConditionAssessmentStatus.Unknown
                : statuses.Contains(ObjectiveConditionAssessmentStatus.Conflicting)
                    ? ObjectiveConditionAssessmentStatus.Conflicting
                    : statuses.All(value => value == ObjectiveConditionAssessmentStatus.Satisfied)
                        ? ObjectiveConditionAssessmentStatus.Satisfied
                        : statuses.Any(value => value == ObjectiveConditionAssessmentStatus.Satisfied)
                            ? ObjectiveConditionAssessmentStatus.PartiallySatisfied
                            : statuses.Contains(ObjectiveConditionAssessmentStatus.Unverified)
                                ? ObjectiveConditionAssessmentStatus.Unverified
                                : statuses.Contains(ObjectiveConditionAssessmentStatus.Stale)
                                    ? ObjectiveConditionAssessmentStatus.Stale
                                    : statuses.All(value => value == ObjectiveConditionAssessmentStatus.Violated)
                                        ? ObjectiveConditionAssessmentStatus.Violated
                                        : ObjectiveConditionAssessmentStatus.Unknown;
    
            return ObjectiveEvaluator.CreateAssessment(condition, status, resolution.Evidence) with
            {
                Preferences = preferences
            };
        }

        private static ObjectiveConditionAssessmentStatus CompareIndividualPreference(
            string preference,
            (
                ObjectiveConditionAssessmentStatus Status,
                ObjectiveConditionValue? Value,
                ImmutableArray<KnowledgeAttribute> Evidence) resolution)
        {
            if (resolution.Status != ObjectiveConditionAssessmentStatus.Satisfied ||
                resolution.Value is null)
            {
                return resolution.Status;
            }
    
            return resolution.Value switch
            {
                ObjectiveTextSetValue available => CompareSetPreference(preference, available),
                ObjectiveTextValue available =>
                    string.Equals(available.Value, preference, StringComparison.OrdinalIgnoreCase)
                        ? ObjectiveConditionAssessmentStatus.Satisfied
                        : ObjectiveConditionAssessmentStatus.Violated,
                _ => ObjectiveConditionAssessmentStatus.Unknown
            };
        }

        private static ObjectiveConditionAssessmentStatus CompareSetPreference(
            string preference,
            ObjectiveTextSetValue available)
        {
            var isIncluded = available.Values.Contains(preference, StringComparer.OrdinalIgnoreCase);
            var isExcluded = available.ExcludedValues.Contains(preference, StringComparer.OrdinalIgnoreCase);
    
            return (isIncluded, isExcluded) switch
            {
                (true, true) => ObjectiveConditionAssessmentStatus.Conflicting,
                (true, false) => ObjectiveConditionAssessmentStatus.Satisfied,
                (false, true) => ObjectiveConditionAssessmentStatus.Violated,
                _ => ObjectiveConditionAssessmentStatus.Unknown
            };
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
                    ? ObjectiveConditionAssessmentStatus.Unknown
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
}
