using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

internal static class ObjectiveTemporalConditionEvaluator
{
        internal static ObjectiveConditionAssessment EvaluateTemporalPlan(
            ObjectiveCondition condition,
            SemanticIdentifier candidate,
            ObjectiveTimeWindowValue window,
            IReadOnlyList<KnowledgeAttribute> attributes)
        {
            if (string.IsNullOrWhiteSpace(condition.TargetAttribute))
            {
                return ObjectiveEvaluator.CreateAssessment(condition, ObjectiveConditionAssessmentStatus.Unknown, []);
            }
    
            var planResolution = ResolveAttributeValue(
                candidate,
                condition.TargetAttribute,
                attributes,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                depth: 0);
    
            if (planResolution.Status != ObjectiveConditionAssessmentStatus.Satisfied)
            {
                return ObjectiveEvaluator.CreateAssessment(condition, planResolution.Status, planResolution.Evidence);
            }
    
            if (planResolution.Value is not ObjectiveTemporalPlanValue plan)
            {
                return ObjectiveEvaluator.CreateAssessment(condition, ObjectiveConditionAssessmentStatus.Unknown, planResolution.Evidence);
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

        internal static (
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
    
                var now = DateTimeOffset.UtcNow;
                var usableEvidence = evidence
                    .Where(attribute => attribute.Sources.Any(source =>
                        source.FreshUntil is { } freshUntil
                            ? freshUntil > now
                            : !source.RequiresFreshness))
                    .ToImmutableArray();
    
                if (usableEvidence.IsDefaultOrEmpty)
                {
                    var sources = evidence.SelectMany(attribute => attribute.Sources).ToArray();
                    var hasUnverifiedSource = sources.Any(source =>
                        source.RequiresFreshness &&
                        source.FreshUntil is null);
                    var hasStaleSource = sources.Any(source =>
                        source.FreshUntil is { } freshUntil &&
                        freshUntil <= now);
                    var status = hasUnverifiedSource
                        ? ObjectiveConditionAssessmentStatus.Unverified
                        : hasStaleSource
                            ? ObjectiveConditionAssessmentStatus.Stale
                            : ObjectiveConditionAssessmentStatus.Unknown;
                    return (status, null, evidence);
                }
    
                evidence = usableEvidence;
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

        internal static ObjectiveConditionAssessment EvaluateTemporalSequence(
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
                return ObjectiveEvaluator.CreateAssessment(condition, ObjectiveConditionAssessmentStatus.Unknown, []);
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
                    return ObjectiveEvaluator.CreateAssessment(condition, resolution.Status, evidence.ToImmutable());
                }
    
                values.Add(name, resolution.Value!);
            }
    
            var resolvedEvidence = evidence.ToImmutable();
    
            foreach (var stage in sequence.Stages)
            {
                if (values[stage.DurationAttribute] is not ObjectiveDurationValue duration || duration.Minutes < 0)
                {
                    return ObjectiveEvaluator.CreateAssessment(condition, ObjectiveConditionAssessmentStatus.Unknown, resolvedEvidence);
                }
    
                if (stage.StartTimesAttribute is not null &&
                    values[stage.StartTimesAttribute] is not ObjectiveTimeSetValue)
                {
                    return ObjectiveEvaluator.CreateAssessment(condition, ObjectiveConditionAssessmentStatus.Unknown, resolvedEvidence);
                }
    
                if (stage.AvailabilityWindowAttribute is not null &&
                    values[stage.AvailabilityWindowAttribute] is not ObjectiveTimeWindowValue)
                {
                    return ObjectiveEvaluator.CreateAssessment(condition, ObjectiveConditionAssessmentStatus.Unknown, resolvedEvidence);
                }
            }
    
            var fits = TryFindTemporalWitness(
                sequence,
                values,
                out var witness);
    
            return ObjectiveEvaluator.CreateAssessment(
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
            out ImmutableArray<ObjectiveTemporalStageResult> witness)
        {
            var memo = new Dictionary<
                (int StageIndex, int PreviousEndMinute),
                (int IdleMinutes, ImmutableArray<ObjectiveTemporalStageResult> Stages)?>();
    
            var best = FindBestWitness(stageIndex: 0, previousEndMinute: -1);
            witness = best?.Stages ?? [];
            return best.HasValue;
    
            (int IdleMinutes, ImmutableArray<ObjectiveTemporalStageResult> Stages)? FindBestWitness(
                int stageIndex,
                int previousEndMinute)
            {
                var key = (stageIndex, previousEndMinute);
                if (memo.TryGetValue(key, out var cached))
                {
                    return cached;
                }
    
                var stage = sequence.Stages[stageIndex];
                var duration = (ObjectiveDurationValue)values[stage.DurationAttribute];
                var earliestStartMinute = (int)sequence.EarliestStart.ToTimeSpan().TotalMinutes;
                var possibleStarts = stage.StartTimesAttribute is null
                    ? new[] { previousEndMinute >= 0 ? previousEndMinute : earliestStartMinute }
                    : ((ObjectiveTimeSetValue)values[stage.StartTimesAttribute])
                        .Times
                        .Select(time => (int)time.ToTimeSpan().TotalMinutes)
                        .Distinct()
                        .Order()
                        .ToArray();
    
                (int IdleMinutes, ImmutableArray<ObjectiveTemporalStageResult> Stages)? bestForState = null;
    
                foreach (var startMinute in possibleStarts)
                {
                    if (startMinute < earliestStartMinute ||
                        (previousEndMinute >= 0 && startMinute < previousEndMinute))
                    {
                        continue;
                    }
    
                    var endMinute = (long)startMinute + duration.Minutes;
                    if (endMinute >= 24 * 60)
                    {
                        continue;
                    }
    
                    var start = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(startMinute));
                    var end = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes((double)endMinute));
    
                    if (stage.AvailabilityWindowAttribute is not null)
                    {
                        var window = (ObjectiveTimeWindowValue)values[stage.AvailabilityWindowAttribute];
                        if (start < window.Start || end > window.End)
                        {
                            continue;
                        }
                    }
    
                    ImmutableArray<ObjectiveTemporalStageResult> remainingStages;
                    var remainingIdleMinutes = 0;
    
                    if (stageIndex == sequence.Stages.Length - 1)
                    {
                        if (end > sequence.LatestEnd)
                        {
                            continue;
                        }
    
                        remainingStages = [];
                    }
                    else
                    {
                        var remaining = FindBestWitness(stageIndex + 1, (int)endMinute);
                        if (!remaining.HasValue)
                        {
                            continue;
                        }
    
                        remainingIdleMinutes = remaining.Value.IdleMinutes;
                        remainingStages = remaining.Value.Stages;
                    }
    
                    var idleMinutes = previousEndMinute >= 0
                        ? startMinute - previousEndMinute
                        : 0;
                    var totalIdleMinutes = idleMinutes + remainingIdleMinutes;
    
                    if (bestForState.HasValue &&
                        totalIdleMinutes >= bestForState.Value.IdleMinutes)
                    {
                        continue;
                    }
    
                    var currentStage = new ObjectiveTemporalStageResult(
                        stageIndex + 1,
                        stage.DurationAttribute,
                        start,
                        end,
                        duration.Minutes);
    
                    bestForState = (
                        totalIdleMinutes,
                        ImmutableArray.Create(currentStage).AddRange(remainingStages));
                }
    
                memo.Add(key, bestForState);
                return bestForState;
            }
        }

        private static bool AreSameValue(ObjectiveConditionValue left, ObjectiveConditionValue right) =>
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
}
