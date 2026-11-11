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
            var hardConditions = candidate.Conditions
                .Where(condition =>
                    condition.Role == ObjectiveConditionRole.HardConstraint &&
                    condition.Status != ObjectiveConditionAssessmentStatus.NotApplicable)
                .ToArray();

            var disposition = hardConditions.Length == 0
                ? "has no comparable hard constraints"
                : candidate.HasHardConstraintViolation
                    ? "fails at least one hard constraint"
                    : candidate.HasUnresolvedHardConstraint
                        ? "has unresolved hard constraints"
                        : "meets all assessed hard constraints";

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

                var sources = condition.Evidence
                    .SelectMany(attribute => attribute.Sources)
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

        return output.ToString().TrimEnd();
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

        if (preferences.Any(condition =>
            condition.Status is ObjectiveConditionAssessmentStatus.Unknown or
                ObjectiveConditionAssessmentStatus.Conflicting))
        {
            return "preference fit is unresolved";
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
