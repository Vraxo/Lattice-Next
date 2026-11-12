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

        return output.ToString().TrimEnd();
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
            return;
        }

        output.AppendLine("Shortlisted options (not ranked):");
        var hasTimetable = false;

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
                output.AppendLine("  No timetable witness is available from the supplied data.");
                continue;
            }

            hasTimetable = true;
            output.AppendLine("  Feasible timetable:");
            TimeOnly? previousEnd = null;

            foreach (var stage in stages)
            {
                if (previousEnd.HasValue && stage.Start > previousEnd.Value)
                {
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
