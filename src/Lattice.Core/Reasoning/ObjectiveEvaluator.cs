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
            .Where(attribute => attribute.IsObjectiveCandidate)
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
                return ObjectiveTemporalConditionEvaluator.EvaluateTemporalSequence(condition, candidate, sequence, attributes);
            }

            if (condition.Value is ObjectiveTimeWindowValue window)
            {
                return ObjectiveTemporalConditionEvaluator.EvaluateTemporalPlan(condition, candidate, window, attributes);
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
            return ObjectiveConditionComparison.EvaluatePreferences(condition, desiredPreferences, resolution);
        }

        if (resolution.Status != ObjectiveConditionAssessmentStatus.Satisfied)
        {
            return CreateAssessment(condition, resolution.Status, resolution.Evidence);
        }

        return CreateAssessment(condition, ObjectiveConditionComparison.Compare(condition, resolution.Value!), resolution.Evidence);
    }



























;

    internal static ObjectiveConditionAssessment CreateAssessment(
        ObjectiveCondition condition,
        ObjectiveConditionAssessmentStatus status,
        ImmutableArray<KnowledgeAttribute> evidence) =>
        new(condition.Name, condition.Role, status, evidence);
}
