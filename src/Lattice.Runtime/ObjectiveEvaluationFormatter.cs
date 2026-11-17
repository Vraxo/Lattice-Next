using System.Globalization;
using System.Text;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

namespace Lattice.Runtime;

internal static class ObjectiveEvaluationFormatter
{
    public static string? Format(ObjectiveEvaluation evaluation)
    {
        ArgumentNullException.ThrowIfNull(evaluation);

        if (evaluation.Candidates.IsDefaultOrEmpty)
        {
            return null;
        }

        var output = new StringBuilder();
        output.AppendLine("Candidate assessment (not ranked):");

        foreach (var candidate in evaluation.Candidates)
        {
            var disposition = candidate.Feasibility switch
            {
                CandidateFeasibility.Eligible => "is eligible under the assessed hard constraints",
                CandidateFeasibility.Excluded => "is excluded by a hard constraint",
                CandidateFeasibility.Unresolved => "has unresolved hard constraints",
                _ => "has no applicable hard constraints"
            };

            var preferenceSummary = DescribePreferences(candidate.Conditions);
            output.Append("- ").Append(candidate.Candidate.Value).Append(": ").Append(disposition);

            if (preferenceSummary is not null)
            {
                output.Append("; ").Append(preferenceSummary);
            }

            output.AppendLine(".");

            foreach (var condition in candidate.Conditions)
            {
                if (condition.Status == ObjectiveConditionAssessmentStatus.NotApplicable)
                {
                    continue;
                }

                output.Append("  - ")
                    .Append(condition.ConditionName)
                    .Append(": ")
                    .Append(DescribeStatus(condition.Status))
                    .AppendLine(".");

                foreach (var preference in condition.Preferences)
                {
                    output.Append("    - ")
                        .Append(preference.Preference)
                        .Append(": ")
                        .Append(DescribeStatus(preference.Status))
                        .AppendLine(".");

                    foreach (var source in preference.Evidence
                        .SelectMany(attribute => attribute.Sources)
                        .Select(source => string.IsNullOrWhiteSpace(source.SourceId)
                            ? source.SourceText
                            : $"{source.SourceText} [source: {source.SourceId}]")
                        .Distinct(StringComparer.Ordinal))
                    {
                        output.Append("      Evidence: ").AppendLine(source);
                    }
                }

                var sources = (condition.Preferences.IsDefaultOrEmpty
                    ? condition.Evidence.SelectMany(attribute => attribute.Sources)
                    : Enumerable.Empty<KnowledgeSource>())
                    .Select(source => string.IsNullOrWhiteSpace(source.SourceId)
                        ? source.SourceText
                        : $"{source.SourceText} [source: {source.SourceId}]")
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                foreach (var source in sources)
                {
                    output.Append("    Evidence: ").AppendLine(source);
                }
            }
        }

        foreach (var tradeOff in evaluation.PreferenceTradeOffs)
        {
            output.Append("- ")
                .Append(tradeOff.CandidateWithPositiveEvidence.Value)
                .Append(" has evidence for ")
                .Append(tradeOff.Preference)
                .Append(", while ")
                .Append(tradeOff.CandidateWithContraryEvidence.Value)
                .AppendLine(" has evidence against it.");

            AppendTradeOffEvidence(output, "  Supporting evidence: ", tradeOff.PositiveEvidence);
            AppendTradeOffEvidence(output, "  Contrary evidence: ", tradeOff.ContraryEvidence);
        }

        ObjectiveEvaluationSummaryFormatter.AppendRecommendation(output, evaluation.Recommendation, evaluation.Candidates);
        ObjectiveEvaluationSummaryFormatter.AppendShortlistedOptions(output, evaluation.Recommendation, evaluation.Candidates);
        AppendEvidenceRequests(output, evaluation.EvidenceRequests, CanProceedWithShortlist(evaluation));
        ObjectiveEvaluationSummaryFormatter.AppendDecisionSummary(output, evaluation);
        ObjectiveEvaluationSummaryFormatter.AppendPlanSummary(output, evaluation);

        return output.ToString().TrimEnd();
    }

    private static void AppendEvidenceRequests(
        StringBuilder output,
        IReadOnlyList<ObjectiveEvidenceRequest> requests,
        bool canProceedWithShortlist)
    {
        if (requests.Count == 0)
        {
            return;
        }

        output.AppendLine(canProceedWithShortlist
            ? "Optional additional candidate evidence:"
            : "Missing hard-constraint evidence:");
        output.Append("- ").AppendLine(requests[0].Prompt);

        var remainingCount = requests.Count - 1;
        if (remainingCount > 0)
        {
            output.Append("I'll ask for the remaining ")
                .Append(remainingCount)
                .AppendLine(remainingCount == 1
                    ? " detail after this one."
                    : " details after this one.");
        }
    }

    internal static bool CanProceedWithShortlist(ObjectiveEvaluation evaluation) =>
        !evaluation.Recommendation.ShortlistedCandidates.IsDefaultOrEmpty &&
        !evaluation.Recommendation.HasIncompletePreferenceEvidence;











    private static void AppendTradeOffEvidence(
        StringBuilder output,
        string prefix,
        IReadOnlyList<KnowledgeAttribute> attributes)
    {
        foreach (var source in attributes
            .SelectMany(attribute => attribute.Sources)
            .Select(source => string.IsNullOrWhiteSpace(source.SourceId)
                ? source.SourceText
                : $"{source.SourceText} [source: {source.SourceId}]")
            .Distinct(StringComparer.Ordinal))
        {
            output.Append(prefix).AppendLine(source);
        }
    }

    private static string? DescribePreferences(
        IReadOnlyList<ObjectiveConditionAssessment> conditions)
    {
        var preferences = conditions
            .Where(condition =>
                condition.Role == ObjectiveConditionRole.Preference &&
                condition.Status != ObjectiveConditionAssessmentStatus.NotApplicable)
            .ToArray();

        if (preferences.Length == 0)
        {
            return null;
        }

        var individualPreferences = preferences
            .SelectMany(condition => condition.Preferences)
            .ToArray();

        if (individualPreferences.Any(preference =>
            preference.Status is ObjectiveConditionAssessmentStatus.Unknown or
                ObjectiveConditionAssessmentStatus.Unverified or
                ObjectiveConditionAssessmentStatus.Stale or
                ObjectiveConditionAssessmentStatus.Conflicting))
        {
            return individualPreferences.Any(preference =>
                preference.Status == ObjectiveConditionAssessmentStatus.Satisfied)
                ? "some preferences are evidenced; others remain unknown"
                : "preference fit is unresolved";
        }

        if (preferences.All(condition =>
            condition.Status == ObjectiveConditionAssessmentStatus.Satisfied))
        {
            return "matches all stated preferences";
        }

        if (preferences.Any(condition =>
            condition.Status == ObjectiveConditionAssessmentStatus.PartiallySatisfied))
        {
            return "partly matches the stated preferences";
        }

        if (preferences.All(condition =>
            condition.Status == ObjectiveConditionAssessmentStatus.Violated))
        {
            return "does not match the stated preferences";
        }

        return "has mixed preference fit";
    }

    internal static string FormatAttributeName(string attributeName)
    {
        var words = attributeName
            .Split(new[] { '-', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..]);
        return string.Join(' ', words);
    }

    internal static string DescribeStatus(ObjectiveConditionAssessmentStatus status) =>
        status switch
        {
            ObjectiveConditionAssessmentStatus.Satisfied => "satisfied",
            ObjectiveConditionAssessmentStatus.Violated => "not satisfied",
            ObjectiveConditionAssessmentStatus.PartiallySatisfied => "partly satisfied",
            ObjectiveConditionAssessmentStatus.Unknown => "unknown from supplied data",
            ObjectiveConditionAssessmentStatus.Unverified => "unverified external evidence; currentness is not established",
            ObjectiveConditionAssessmentStatus.Stale => "stale external evidence; refresh required",
            ObjectiveConditionAssessmentStatus.Conflicting => "conflicting evidence",
            ObjectiveConditionAssessmentStatus.NotApplicable => "not applicable",
            _ => "unrecognized status"
        };
}
