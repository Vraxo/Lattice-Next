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

        return new ObjectiveEvaluation(candidates)
        {
            PreferenceTradeOffs = BuildPreferenceTradeOffs(candidates),
            Recommendation = BuildRecommendation(candidates),
            EvidenceRequests = BuildEvidenceRequests(objective, candidates)
        };
    }

    private static ImmutableArray<ObjectiveEvidenceRequest> BuildEvidenceRequests(
        ObjectiveFormula objective,
        ImmutableArray<CandidateAssessment> candidates)
    {
        var result = ImmutableArray.CreateBuilder<ObjectiveEvidenceRequest>();

        foreach (var candidate in candidates)
        {
            if (candidate.Feasibility != CandidateFeasibility.Unresolved)
            {
                continue;
            }

            for (var index = 0; index < objective.Conditions.Length; index++)
            {
                var condition = objective.Conditions[index];
                var assessment = candidate.Conditions[index];

                var needsEvidence = assessment.Status is
                    ObjectiveConditionAssessmentStatus.Unknown or
                    ObjectiveConditionAssessmentStatus.Unverified or
                    ObjectiveConditionAssessmentStatus.Stale;

                if (condition.Role != ObjectiveConditionRole.HardConstraint ||
                    condition.Value is null ||
                    !needsEvidence ||
                    (assessment.Status == ObjectiveConditionAssessmentStatus.Unknown &&
                        !assessment.Evidence.IsDefaultOrEmpty) ||
                    string.IsNullOrWhiteSpace(condition.TargetAttribute))
                {
                    continue;
                }

                var attributeName = string.Join(
                    ' ',
                    condition.TargetAttribute.Split('-', StringSplitOptions.RemoveEmptyEntries));
                var prompt = $"What is the {attributeName} for {candidate.Candidate.Value}?";

                result.Add(new ObjectiveEvidenceRequest(
                    candidate.Candidate,
                    condition.Name,
                    condition.TargetAttribute,
                    prompt));
            }
        }

        return result.ToImmutable();
    }

    private static ImmutableArray<ObjectivePreferenceTradeOff> BuildPreferenceTradeOffs(
        ImmutableArray<CandidateAssessment> candidates)
    {
        var result = ImmutableArray.CreateBuilder<ObjectivePreferenceTradeOff>();
        for (var i = 0; i < candidates.Length; i++)
        {
            var left = candidates[i];
            if (left.Feasibility != CandidateFeasibility.Eligible) continue;
            var leftItems = left.Conditions.Where(x => x.Role == ObjectiveConditionRole.Preference)
                .SelectMany(x => x.Preferences)
                .GroupBy(x => x.Preference, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
            for (var j = i + 1; j < candidates.Length; j++)
            {
                var right = candidates[j];
                if (right.Feasibility != CandidateFeasibility.Eligible) continue;
                var rightItems = right.Conditions.Where(x => x.Role == ObjectiveConditionRole.Preference)
                    .SelectMany(x => x.Preferences)
                .GroupBy(x => x.Preference, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
                foreach (var item in leftItems)
                {
                    if (!rightItems.TryGetValue(item.Key, out var other)) continue;
                    if (item.Value.Status == ObjectiveConditionAssessmentStatus.Satisfied && other.Status == ObjectiveConditionAssessmentStatus.Violated)
                        result.Add(new ObjectivePreferenceTradeOff(item.Key, left.Candidate, right.Candidate, item.Value.Evidence, other.Evidence));
                    else if (other.Status == ObjectiveConditionAssessmentStatus.Satisfied && item.Value.Status == ObjectiveConditionAssessmentStatus.Violated)
                        result.Add(new ObjectivePreferenceTradeOff(item.Key, right.Candidate, left.Candidate, other.Evidence, item.Value.Evidence));
                }
            }
        }
        return result.ToImmutable();
    }
    private static ObjectiveRecommendation BuildRecommendation(
        ImmutableArray<CandidateAssessment> candidates)
    {
        var eligible = candidates
            .Where(candidate => candidate.Feasibility == CandidateFeasibility.Eligible)
            .ToImmutableArray();

        if (eligible.IsDefaultOrEmpty)
        {
            return new ObjectiveRecommendation(
                ObjectiveRecommendationStatus.NoEligibleCandidates,
                [],
                [],
                HasIncompletePreferenceEvidence: true);
        }

        if (eligible.Length == 1)
        {
            return new ObjectiveRecommendation(
                ObjectiveRecommendationStatus.OnlyEligibleCandidate,
                [eligible[0].Candidate],
                [],
                HasIncompletePreferenceEvidence: HasIncompletePreferences(eligible));
        }

        var preferences = eligible
            .SelectMany(candidate => candidate.Conditions)
            .Where(condition => condition.Role == ObjectiveConditionRole.Preference)
            .SelectMany(condition => condition.Preferences)
            .Select(preference => preference.Preference)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var dominated = eligible
            .Where(candidate => eligible.Any(other =>
                other.Candidate != candidate.Candidate &&
                Dominates(other, candidate, preferences)))
            .Select(candidate => candidate.Candidate)
            .ToImmutableArray();

        var frontier = eligible
            .Where(candidate => !dominated.Contains(candidate.Candidate))
            .Select(candidate => candidate.Candidate)
            .ToImmutableArray();

        return new ObjectiveRecommendation(
            frontier.Length == 1
                ? ObjectiveRecommendationStatus.UniquePreferenceLeader
                : ObjectiveRecommendationStatus.MultipleNonDominatedCandidates,
            frontier,
            dominated,
            HasIncompletePreferenceEvidence: preferences.Length == 0 ||
                HasIncompletePreferences(eligible, preferences));
    }

    private static bool Dominates(
        CandidateAssessment candidate,
        CandidateAssessment other,
        IReadOnlyList<string> preferences)
    {
        if (preferences.Count == 0)
        {
            return false;
        }

        var candidatePreferences = GetPreferenceMap(candidate);
        var otherPreferences = GetPreferenceMap(other);
        var isStrictlyBetter = false;

        foreach (var preference in preferences)
        {
            if (!candidatePreferences.TryGetValue(preference, out var candidateAssessment) ||
                !otherPreferences.TryGetValue(preference, out var otherAssessment))
            {
                return false;
            }

            if (candidateAssessment.Status is not (
                ObjectiveConditionAssessmentStatus.Satisfied or ObjectiveConditionAssessmentStatus.Violated) ||
                otherAssessment.Status is not (
                ObjectiveConditionAssessmentStatus.Satisfied or ObjectiveConditionAssessmentStatus.Violated))
            {
                return false;
            }

            if (candidateAssessment.Status == ObjectiveConditionAssessmentStatus.Violated &&
                otherAssessment.Status == ObjectiveConditionAssessmentStatus.Satisfied)
            {
                return false;
            }

            if (candidateAssessment.Status == ObjectiveConditionAssessmentStatus.Satisfied &&
                otherAssessment.Status == ObjectiveConditionAssessmentStatus.Violated)
            {
                isStrictlyBetter = true;
            }
        }

        return isStrictlyBetter;
    }

    private static Dictionary<string, ObjectivePreferenceAssessment> GetPreferenceMap(
        CandidateAssessment candidate) =>
        candidate.Conditions
            .Where(condition => condition.Role == ObjectiveConditionRole.Preference)
            .SelectMany(condition => condition.Preferences)
            .GroupBy(preference => preference.Preference, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

    private static bool HasIncompletePreferences(ImmutableArray<CandidateAssessment> candidates) =>
        HasIncompletePreferences(
            candidates,
            candidates.SelectMany(candidate => candidate.Conditions)
                .Where(condition => condition.Role == ObjectiveConditionRole.Preference)
                .SelectMany(condition => condition.Preferences)
                .Select(preference => preference.Preference)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray());

    private static bool HasIncompletePreferences(
        ImmutableArray<CandidateAssessment> candidates,
        IReadOnlyList<string> preferences)
    {
        if (preferences.Count == 0)
        {
            return true;
        }

        return candidates.Any(candidate =>
        {
            var assessments = GetPreferenceMap(candidate);
            return preferences.Any(preference =>
                !assessments.TryGetValue(preference, out var assessment) ||
                assessment.Status is not (
                    ObjectiveConditionAssessmentStatus.Satisfied or ObjectiveConditionAssessmentStatus.Violated));
        });
    }

    private static CandidateAssessment EvaluateCandidate(
        ObjectiveFormula objective,
        SemanticIdentifier candidate,
        IReadOnlyList<KnowledgeAttribute> attributes)
    {
        var conditions = objective.Conditions
            .Select(condition => EvaluateCondition(condition, candidate, attributes))
            .ToImmutableArray();

        return new CandidateAssessment(candidate, conditions)
        {
            Highlights = attributes
                .Where(attribute => attribute.Subject == candidate && attribute.IncludeInSummary)
                .ToImmutableArray()
        };
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

    private static ObjectiveConditionAssessment CreateAssessment(
        ObjectiveCondition condition,
        ObjectiveConditionAssessmentStatus status,
        ImmutableArray<KnowledgeAttribute> evidence) =>
        new(condition.Name, condition.Role, status, evidence);
}
