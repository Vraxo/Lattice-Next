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

        AppendRecommendation(output, evaluation.Recommendation, evaluation.Candidates);
        AppendShortlistedOptions(output, evaluation.Recommendation, evaluation.Candidates);
        AppendEvidenceRequests(output, evaluation.EvidenceRequests);
        AppendDecisionSummary(output, evaluation);
        AppendPlanSummary(output, evaluation);

        return output.ToString().TrimEnd();
    }

    private static void AppendEvidenceRequests(
        StringBuilder output,
        IReadOnlyList<ObjectiveEvidenceRequest> requests)
    {
        if (requests.Count == 0)
        {
            return;
        }

        output.AppendLine("Missing hard-constraint evidence:");
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

    private static void AppendDecisionSummary(
        StringBuilder output,
        ObjectiveEvaluation evaluation)
    {
        var recommendation = evaluation.Recommendation;
        var options = recommendation.ShortlistedCandidates
            .Select(name => evaluation.Candidates.FirstOrDefault(candidate => candidate.Candidate == name))
            .Where(candidate => candidate is not null)
            .Cast<CandidateAssessment>()
            .ToArray();

        output.AppendLine("Decision summary:");

        var candidatesWithPendingRequests = evaluation.EvidenceRequests
            .Select(request => request.Candidate.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var preferenceCandidates = options.Length > 0
            ? options
            : evaluation.Candidates
                .Where(candidate => !candidatesWithPendingRequests.Contains(candidate.Candidate.Value))
                .ToArray();

        if (options.Length > 0)
        {
            if (options.All(candidate => candidate.Feasibility == CandidateFeasibility.Eligible))
            {
                output.AppendLine("Confirmed: all shortlisted options satisfy the assessed hard constraints.");
            }
            else
            {
                output.AppendLine("Confirmed: the shortlist contains options with no known hard-constraint violation.");
            }
        }

        var preferenceEvidence = preferenceCandidates
            .SelectMany(candidate => candidate.Conditions
                .Where(condition => condition.Role == ObjectiveConditionRole.Preference)
                .SelectMany(condition => condition.Preferences.Select(preference =>
                    (Candidate: candidate.Candidate.Value, preference.Preference, preference.Status))))
            .ToArray();

        var unknownPreferences = preferenceEvidence
            .Where(item => item.Status is ObjectiveConditionAssessmentStatus.Unknown or ObjectiveConditionAssessmentStatus.Conflicting)
            .GroupBy(item => item.Preference, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
                $"{FormatAttributeName(group.Key)} ({string.Join(", ", group.Select(item => item.Candidate).Distinct(StringComparer.Ordinal))})")
            .ToArray();

        if (unknownPreferences.Length > 0)
        {
            output.Append("Still unknown: ")
                .Append(string.Join("; ", unknownPreferences))
                .AppendLine(".");
        }
        else if (preferenceEvidence.Length > 0)
        {
            output.AppendLine("Unknowns: no missing or conflicting evidence for the stated preferences of shortlisted options.");
        }

        var violatedPreferences = preferenceEvidence
            .Where(item => item.Status == ObjectiveConditionAssessmentStatus.Violated)
            .GroupBy(item => item.Preference, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
                $"{FormatAttributeName(group.Key)} ({string.Join(", ", group.Select(item => item.Candidate).Distinct(StringComparer.Ordinal))})")
            .ToArray();

        if (violatedPreferences.Length > 0)
        {
            output.Append("Known preference mismatches: ")
                .Append(string.Join("; ", violatedPreferences))
                .AppendLine(".");
        }

        if (options.Length == 0)
        {
            output.AppendLine(evaluation.EvidenceRequests.Length > 0
                ? "Next step: answer the missing candidate detail above."
                : "Next step: review the excluded constraints and decide whether one can be relaxed.");
            return;
        }

        var allPreferencesSatisfied = preferenceEvidence.Length > 0 &&
            preferenceEvidence.All(item => item.Status == ObjectiveConditionAssessmentStatus.Satisfied);
        var shortlistedNames = recommendation.ShortlistedCandidates.ToHashSet();
        var hasTradeOff = evaluation.PreferenceTradeOffs.Any(tradeOff =>
            shortlistedNames.Contains(tradeOff.CandidateWithPositiveEvidence) &&
            shortlistedNames.Contains(tradeOff.CandidateWithContraryEvidence));

        if (allPreferencesSatisfied && options.Length > 1)
        {
            output.AppendLine("All shortlisted candidates satisfy the stated preferences. Cost and travel time are shown, but neither is used to rank options because no such priority was stated.");
            output.AppendLine("No preference-based winner is established.");
        }
        else if (hasTradeOff)
        {
            output.AppendLine("Different options have evidence-backed advantages on different preferences; no weights are assumed.");
        }
        else if (recommendation.HasIncompletePreferenceEvidence)
        {
            output.AppendLine("Preference evidence is incomplete, so the shortlist is provisional.");
        }

        switch (recommendation.Status)
        {
            case ObjectiveRecommendationStatus.MultipleNonDominatedCandidates:
                if (allPreferencesSatisfied)
                {
                    output.AppendLine("Next step: choose one of the shortlisted options, or state an additional priority to distinguish them.");
                }
                else if (hasTradeOff)
                {
                    output.AppendLine("Next step: tell me which preference should take priority.");
                }
                else
                {
                    output.AppendLine("Next step: provide more preference evidence or choose based on the information currently available.");
                }

                break;

            case ObjectiveRecommendationStatus.OnlyEligibleCandidate:
                output.AppendLine(violatedPreferences.Length > 0
                    ? "Next step: decide whether to accept that preference compromise or relax a hard constraint."
                    : recommendation.HasIncompletePreferenceEvidence
                        ? "Next step: provide evidence for the remaining preferences or confirm whether the current option is acceptable."
                        : "Next step: confirm whether this option is acceptable.");
                break;

            case ObjectiveRecommendationStatus.UniquePreferenceLeader:
                output.AppendLine("Next step: review the leading option and confirm whether it is acceptable.");
                break;

        }
    }

    private static void AppendPlanSummary(StringBuilder output, ObjectiveEvaluation evaluation)
    {
        var options = evaluation.Recommendation.ShortlistedCandidates
            .Select(name => evaluation.Candidates.FirstOrDefault(candidate => candidate.Candidate == name))
            .Where(candidate => candidate is not null)
            .Cast<CandidateAssessment>()
            .ToArray();

        if (options.Length == 0 ||
            !options.Any(candidate => candidate.Conditions
                .SelectMany(condition => condition.TemporalWitness)
                .Any()))
        {
            return;
        }

        output.AppendLine("Plan summary (based on supplied evidence):");
        output.Append("  Supported details: ")
            .Append(string.Join(", ", options.Select(candidate => candidate.Candidate.Value)))
            .AppendLine("; feasible timetable stages are listed above.");

        output.AppendLine("  Assumptions: no additional assumptions were introduced. The timetable is a first feasible witness from supplied data, not an optimized or live-verified schedule.");

        var unresolvedCandidates = evaluation.Candidates
            .Where(candidate => candidate.Feasibility == CandidateFeasibility.Unresolved)
            .Select(candidate => $"{candidate.Candidate.Value}: hard-constraint evidence is unresolved");
        var unresolvedConditions = options
            .SelectMany(candidate => candidate.Conditions
                .Where(condition => condition.Status is ObjectiveConditionAssessmentStatus.Unknown or ObjectiveConditionAssessmentStatus.Conflicting)
                .Select(condition => $"{candidate.Candidate.Value}: {condition.ConditionName} is {DescribeStatus(condition.Status)}"));
        var unresolvedPreferences = options
            .SelectMany(candidate => candidate.Conditions
                .SelectMany(condition => condition.Preferences
                    .Where(preference => preference.Status is ObjectiveConditionAssessmentStatus.Unknown or ObjectiveConditionAssessmentStatus.Conflicting)
                    .Select(preference => $"{candidate.Candidate.Value}: {preference.Preference} is {DescribeStatus(preference.Status)}")));
        var unknowns = unresolvedCandidates
            .Concat(unresolvedConditions)
            .Concat(unresolvedPreferences)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        output.Append("  Unknowns: ");
        if (unknowns.Length > 0)
        {
            output.Append(string.Join("; ", unknowns)).Append(". ");
        }
        else
        {
            output.Append("no assessed candidate constraint or preference remains unresolved. ");
        }

        output.AppendLine("Live availability, current opening hours, transport departures, weather, and booking requirements are not verified by this evaluation.");

        output.Append("  Your actions: ");
        if (evaluation.EvidenceRequests.Length > 0)
        {
            output.Append("answer the outstanding evidence request above; ");
        }

        output.AppendLine("verify live details before acting and complete any required booking yourself. No booking has been made.");
    }

    private static void AppendShortlistedOptions(
        StringBuilder output,
        ObjectiveRecommendation recommendation,
        IReadOnlyList<CandidateAssessment> candidates)
    {
        var options = recommendation.ShortlistedCandidates
            .Select(name => candidates.FirstOrDefault(candidate => candidate.Candidate == name))
            .Where(candidate => candidate is not null)
            .Cast<CandidateAssessment>()
            .ToArray();

        if (options.Length == 0)
        {
            var knownCandidates = candidates
                .Where(candidate => !candidate.Highlights.IsDefaultOrEmpty)
                .ToArray();

            if (knownCandidates.Length > 0)
            {
                output.AppendLine("Known candidate details (not recommendations):");
                foreach (var candidate in knownCandidates)
                {
                    output.Append("- ").AppendLine(candidate.Candidate.Value);
                    foreach (var highlight in candidate.Highlights)
                    {
                        foreach (var source in highlight.Sources)
                        {
                            output.Append("  ")
                                .Append(FormatAttributeName(highlight.Name.Value))
                                .Append(": ")
                                .AppendLine(source.SourceText);
                        }
                    }
                }
            }

            return;
        }

        output.AppendLine("Shortlisted options (not ranked):");
        var hasTimetable = false;
        var hasUnscheduledGap = false;

        foreach (var candidate in options)
        {
            output.Append("- ").AppendLine(candidate.Candidate.Value);

            foreach (var highlight in candidate.Highlights)
            {
                foreach (var source in highlight.Sources)
                {
                    output.Append("  ")
                        .Append(FormatAttributeName(highlight.Name.Value))
                        .Append(": ")
                        .AppendLine(source.SourceText);
                }
            }

            var stages = candidate.Conditions
                .SelectMany(condition => condition.TemporalWitness)
                .ToArray();

            if (stages.Length == 0)
            {
                continue;
            }

            hasTimetable = true;
            output.AppendLine("  Feasible timetable:");
            TimeOnly? previousEnd = null;

            foreach (var stage in stages)
            {
                if (previousEnd.HasValue && stage.Start > previousEnd.Value)
                {
                    hasUnscheduledGap = true;
                    output.Append("    Unscheduled time: ")
                        .Append(previousEnd.Value.ToString("HH:mm", CultureInfo.InvariantCulture))
                        .Append('–')
                        .AppendLine(stage.Start.ToString("HH:mm", CultureInfo.InvariantCulture));
                }

                output.Append("    Stage ")
                    .Append(stage.Index)
                    .Append(": ")
                    .Append(stage.Start.ToString("HH:mm", CultureInfo.InvariantCulture))
                    .Append('–')
                    .Append(stage.End.ToString("HH:mm", CultureInfo.InvariantCulture))
                    .Append(" — ")
                    .Append(FormatAttributeName(stage.DurationAttribute))
                    .Append(" (")
                    .Append(stage.DurationMinutes)
                    .AppendLine(" min).");

                previousEnd = stage.End;
            }
        }

        if (hasTimetable)
        {
            output.AppendLine("Timetables are first feasible witnesses, not optimized schedules.");
        }

        if (hasUnscheduledGap)
        {
            output.AppendLine("Assumptions: unscheduled gaps are left flexible; no additional activity or meal slot is assumed.");
        }
    }

    private static void AppendRecommendation(
        StringBuilder output,
        ObjectiveRecommendation recommendation,
        IReadOnlyList<CandidateAssessment> candidates)
    {
        var unresolvedCandidates = candidates
            .Where(candidate => candidate.Feasibility == CandidateFeasibility.Unresolved)
            .Select(candidate => candidate.Candidate.Value)
            .ToArray();

        switch (recommendation.Status)
        {
            case ObjectiveRecommendationStatus.NoEligibleCandidates:
                output.AppendLine("No candidate is currently confirmed eligible under the assessed hard constraints.");
                if (unresolvedCandidates.Length > 0)
                {
                    output.Append("  Unresolved candidates still need evidence: ")
                        .AppendLine(string.Join(", ", unresolvedCandidates));
                }

                break;

            case ObjectiveRecommendationStatus.OnlyEligibleCandidate:
                output.Append("Only currently eligible candidate: ")
                    .Append(recommendation.ShortlistedCandidates[0].Value)
                    .AppendLine(".");
                break;

            case ObjectiveRecommendationStatus.UniquePreferenceLeader:
                output.Append("Only non-dominated eligible candidate on the stated preferences: ")
                    .Append(recommendation.ShortlistedCandidates[0].Value)
                    .AppendLine(".");
                break;

            case ObjectiveRecommendationStatus.MultipleNonDominatedCandidates:
                output.Append("No unique preference leader; these eligible candidates remain non-dominated: ")
                    .Append(string.Join(", ", recommendation.ShortlistedCandidates.Select(candidate => candidate.Value)))
                    .AppendLine(".");

                break;
        }

        if (recommendation.HasIncompletePreferenceEvidence)
        {
            output.AppendLine("  Preference evidence is incomplete, so this shortlist is not a complete comparison.");
        }

        if (unresolvedCandidates.Length > 0 &&
            recommendation.Status != ObjectiveRecommendationStatus.NoEligibleCandidates)
        {
            output.Append("  Other candidates remain unresolved and were not included: ")
                .AppendLine(string.Join(", ", unresolvedCandidates));
        }
    }

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

    private static string FormatAttributeName(string attributeName)
    {
        var words = attributeName
            .Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..]);
        return string.Join(' ', words);
    }

    private static string DescribeStatus(ObjectiveConditionAssessmentStatus status) =>
        status switch
        {
            ObjectiveConditionAssessmentStatus.Satisfied => "satisfied",
            ObjectiveConditionAssessmentStatus.Violated => "not satisfied",
            ObjectiveConditionAssessmentStatus.PartiallySatisfied => "partly satisfied",
            ObjectiveConditionAssessmentStatus.Unknown => "unknown from supplied data",
            ObjectiveConditionAssessmentStatus.Conflicting => "conflicting evidence",
            ObjectiveConditionAssessmentStatus.NotApplicable => "not applicable",
            _ => "unrecognized status"
        };
}
