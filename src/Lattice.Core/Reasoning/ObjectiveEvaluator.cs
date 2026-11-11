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
        if (condition.Operator == ObjectiveConditionOperator.CanFit)
        {
            if (condition.Value is ObjectiveTemporalSequenceValue sequence)
            {
                return EvaluateTemporalSequence(condition, candidate, sequence, attributes);
            }

            if (condition.Value is ObjectiveTimeWindowValue window)
            {
                return EvaluateTemporalPlan(condition, candidate, window, attributes);
            }
        }

        if (string.IsNullOrWhiteSpace(condition.TargetAttribute))
        {
            return CreateAssessment(condition, ObjectiveConditionAssessmentStatus.NotApplicable, []);
        }

        var resolution = ResolveAttributeValue(
            candidate,
            condition.TargetAttribute,
            attributes,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            depth: 0);

        if (condition.Operator == ObjectiveConditionOperator.Desires &&
            condition.Value is ObjectiveTextSetValue desiredPreferences)
        {
            return EvaluatePreferences(condition, desiredPreferences, resolution);
        }

        if (resolution.Status != ObjectiveConditionAssessmentStatus.Satisfied)
        {
            return CreateAssessment(condition, resolution.Status, resolution.Evidence);
        }

        return CreateAssessment(condition, Compare(condition, resolution.Value!), resolution.Evidence);
    }

    private static ObjectiveConditionAssessment EvaluateTemporalPlan(
        ObjectiveCondition condition,
        SemanticIdentifier candidate,
        ObjectiveTimeWindowValue window,
        IReadOnlyList<KnowledgeAttribute> attributes)
    {
        if (string.IsNullOrWhiteSpace(condition.TargetAttribute))
        {
            return CreateAssessment(condition, ObjectiveConditionAssessmentStatus.Unknown, []);
        }

        var planResolution = ResolveAttributeValue(
            candidate,
            condition.TargetAttribute,
            attributes,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            depth: 0);

        if (planResolution.Status != ObjectiveConditionAssessmentStatus.Satisfied)
        {
            return CreateAssessment(condition, planResolution.Status, planResolution.Evidence);
        }

        if (planResolution.Value is not ObjectiveTemporalPlanValue plan)
        {
            return CreateAssessment(condition, ObjectiveConditionAssessmentStatus.Unknown, planResolution.Evidence);
        }

        var sequence = new ObjectiveTemporalSequenceValue(
            window.Start,
            window.End,
            plan.Stages);
        var assessment = EvaluateTemporalSequence(
            condition with { Value = sequence },
            candidate,
            sequence,
            attributes);

        return assessment with
        {
            Evidence = assessment.Evidence.AddRange(planResolution.Evidence)
        };
    }

    private static (
        ObjectiveConditionAssessmentStatus Status,
        ObjectiveConditionValue? Value,
        ImmutableArray<KnowledgeAttribute> Evidence) ResolveAttributeValue(
            SemanticIdentifier candidate,
            string attributeName,
            IReadOnlyList<KnowledgeAttribute> attributes,
            HashSet<string> resolving,
            int depth)
    {
        if (depth >= 16 || !resolving.Add(attributeName))
        {
            return (ObjectiveConditionAssessmentStatus.Unknown, null, []);
        }

        try
        {
            var evidence = attributes
                .Where(attribute =>
                    attribute.Subject == candidate &&
                    string.Equals(attribute.Name.Value, attributeName, StringComparison.OrdinalIgnoreCase))
                .ToImmutableArray();

            if (evidence.IsDefaultOrEmpty)
            {
                return (ObjectiveConditionAssessmentStatus.Unknown, null, evidence);
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
                return (ObjectiveConditionAssessmentStatus.Conflicting, null, evidence);
            }

            if (distinctValues[0] is not ObjectiveSumValue sum)
            {
                return (ObjectiveConditionAssessmentStatus.Satisfied, distinctValues[0], evidence);
            }

            if (sum.Attributes.Length is < 2 or > 64 ||
                sum.Attributes.Any(string.IsNullOrWhiteSpace) ||
                sum.Attributes.Distinct(StringComparer.OrdinalIgnoreCase).Count() != sum.Attributes.Length)
            {
                return (ObjectiveConditionAssessmentStatus.Unknown, null, evidence);
            }

            var allEvidence = ImmutableArray.CreateBuilder<KnowledgeAttribute>();
            allEvidence.AddRange(evidence);
            decimal moneyTotal = 0m;
            var durationTotal = 0;
            string? currencySymbol = null;
            var hasMoney = false;
            var hasDuration = false;

            foreach (var componentName in sum.Attributes)
            {
                var component = ResolveAttributeValue(
                    candidate,
                    componentName,
                    attributes,
                    resolving,
                    depth + 1);
                allEvidence.AddRange(component.Evidence);

                if (component.Status != ObjectiveConditionAssessmentStatus.Satisfied)
                {
                    return (component.Status, null, allEvidence.ToImmutable());
                }

                switch (component.Value)
                {
                    case ObjectiveMoneyValue money:
                        if (hasDuration ||
                            (currencySymbol is not null &&
                                !string.Equals(currencySymbol, money.CurrencySymbol, StringComparison.Ordinal)))
                        {
                            return (ObjectiveConditionAssessmentStatus.Unknown, null, allEvidence.ToImmutable());
                        }

                        hasMoney = true;
                        currencySymbol ??= money.CurrencySymbol;

                        try
                        {
                            moneyTotal = checked(moneyTotal + money.Amount);
                        }
                        catch (OverflowException)
                        {
                            return (ObjectiveConditionAssessmentStatus.Unknown, null, allEvidence.ToImmutable());
                        }

                        break;

                    case ObjectiveDurationValue duration:
                        if (hasMoney)
                        {
                            return (ObjectiveConditionAssessmentStatus.Unknown, null, allEvidence.ToImmutable());
                        }

                        hasDuration = true;

                        try
                        {
                            durationTotal = checked(durationTotal + duration.Minutes);
                        }
                        catch (OverflowException)
                        {
                            return (ObjectiveConditionAssessmentStatus.Unknown, null, allEvidence.ToImmutable());
                        }

                        break;

                    default:
                        return (ObjectiveConditionAssessmentStatus.Unknown, null, allEvidence.ToImmutable());
                }
            }

            ObjectiveConditionValue total = hasMoney
                ? new ObjectiveMoneyValue(moneyTotal, currencySymbol!)
                : new ObjectiveDurationValue(durationTotal);
            return (ObjectiveConditionAssessmentStatus.Satisfied, total, allEvidence.ToImmutable());
        }
        finally
        {
            resolving.Remove(attributeName);
        }
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
                stage is null ||
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

        var evidence = ImmutableArray.CreateBuilder<KnowledgeAttribute>();
        var values = new Dictionary<string, ObjectiveConditionValue>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in requiredNames)
        {
            var resolution = ResolveAttributeValue(
                candidate,
                name,
                attributes,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                depth: 0);
            evidence.AddRange(resolution.Evidence);

            if (resolution.Status != ObjectiveConditionAssessmentStatus.Satisfied)
            {
                return CreateAssessment(condition, resolution.Status, evidence.ToImmutable());
            }

            values.Add(name, resolution.Value!);
        }

        var resolvedEvidence = evidence.ToImmutable();

        foreach (var stage in sequence.Stages)
        {
            if (values[stage.DurationAttribute] is not ObjectiveDurationValue duration || duration.Minutes < 0)
            {
                return CreateAssessment(condition, ObjectiveConditionAssessmentStatus.Unknown, resolvedEvidence);
            }

            if (stage.StartTimesAttribute is not null &&
                values[stage.StartTimesAttribute] is not ObjectiveTimeSetValue)
            {
                return CreateAssessment(condition, ObjectiveConditionAssessmentStatus.Unknown, resolvedEvidence);
            }

            if (stage.AvailabilityWindowAttribute is not null &&
                values[stage.AvailabilityWindowAttribute] is not ObjectiveTimeWindowValue)
            {
                return CreateAssessment(condition, ObjectiveConditionAssessmentStatus.Unknown, resolvedEvidence);
            }
        }

        var fits = TryFindTemporalWitness(
            sequence,
            values,
            stageIndex: 0,
            previousEnd: null,
            priorStages: [],
            out var witness);

        return CreateAssessment(
            condition,
            fits ? ObjectiveConditionAssessmentStatus.Satisfied : ObjectiveConditionAssessmentStatus.Violated,
            resolvedEvidence) with
        {
            TemporalWitness = witness
        };
    }

    private static bool TryFindTemporalWitness(
        ObjectiveTemporalSequenceValue sequence,
        IReadOnlyDictionary<string, ObjectiveConditionValue> values,
        int stageIndex,
        TimeOnly? previousEnd,
        ImmutableArray<ObjectiveTemporalStageResult> priorStages,
        out ImmutableArray<ObjectiveTemporalStageResult> witness)
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

            var currentStages = priorStages.Add(new ObjectiveTemporalStageResult(
                stageIndex + 1,
                stage.DurationAttribute,
                start,
                end,
                duration.Minutes));

            if (stageIndex == sequence.Stages.Length - 1)
            {
                if (end <= sequence.LatestEnd)
                {
                    witness = currentStages;
                    return true;
                }

                continue;
            }

            if (TryFindTemporalWitness(
                sequence,
                values,
                stageIndex + 1,
                end,
                currentStages,
                out witness))
            {
                return true;
            }
        }

        witness = [];
        return false;
    }

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
            .Select(preference => new ObjectivePreferenceAssessment(
                preference,
                CompareIndividualPreference(preference, resolution),
                resolution.Evidence))
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
                        : statuses.All(value => value == ObjectiveConditionAssessmentStatus.Violated)
                            ? ObjectiveConditionAssessmentStatus.Violated
                            : ObjectiveConditionAssessmentStatus.Unknown;

        return CreateAssessment(condition, status, resolution.Evidence) with
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
            ObjectiveTextSetValue available =>
                available.Values.Contains(preference, StringComparer.OrdinalIgnoreCase)
                    ? ObjectiveConditionAssessmentStatus.Satisfied
                    : ObjectiveConditionAssessmentStatus.Unknown,
            ObjectiveTextValue available =>
                string.Equals(available.Value, preference, StringComparison.OrdinalIgnoreCase)
                    ? ObjectiveConditionAssessmentStatus.Satisfied
                    : ObjectiveConditionAssessmentStatus.Violated,
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

    private static bool AreSameValue(ObjectiveConditionValue left, ObjectiveConditionValue right) =>
        (left, right) switch
        {
            (ObjectiveTextSetValue leftSet, ObjectiveTextSetValue rightSet) =>
                leftSet.Values.SequenceEqual(rightSet.Values, StringComparer.OrdinalIgnoreCase),
            (ObjectiveTimeSetValue leftSet, ObjectiveTimeSetValue rightSet) =>
                leftSet.Times.SequenceEqual(rightSet.Times),
            (ObjectiveTemporalPlanValue leftPlan, ObjectiveTemporalPlanValue rightPlan) =>
                leftPlan.Stages.SequenceEqual(rightPlan.Stages),
            (ObjectiveSumValue leftSum, ObjectiveSumValue rightSum) =>
                leftSum.Attributes.SequenceEqual(rightSum.Attributes, StringComparer.OrdinalIgnoreCase),
            _ => Equals(left, right)
        };

    private static ObjectiveConditionAssessment CreateAssessment(
        ObjectiveCondition condition,
        ObjectiveConditionAssessmentStatus status,
        ImmutableArray<KnowledgeAttribute> evidence) =>
        new(condition.Name, condition.Role, status, evidence);
}
