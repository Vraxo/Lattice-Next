using System.Globalization;
using System.Text;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

namespace Lattice.Runtime;

internal static class ObjectiveResponseFormatter
{
    public static string? Format(ObjectiveEvaluation evaluation)
    {
        ArgumentNullException.ThrowIfNull(evaluation);

        if (evaluation.Candidates.IsDefaultOrEmpty)
        {
            return null;
        }

        var options = evaluation.Recommendation.ShortlistedCandidates
            .Select(name => evaluation.Candidates.FirstOrDefault(candidate => candidate.Candidate == name))
            .Where(candidate => candidate is not null)
            .Cast<CandidateAssessment>()
            .ToArray();
        var output = new StringBuilder();

        if (options.Length == 0)
        {
            AppendNoRecommendation(output, evaluation);
            return output.ToString().TrimEnd();
        }

        AppendOpening(output, evaluation, options);
        AppendOptions(output, options);
        AppendPreferenceTradeOff(output, evaluation, options);
        AppendPreferenceMismatches(output, evaluation, options);
        AppendOptionalCandidates(output, evaluation, options);

        if (evaluation.Recommendation.HasIncompletePreferenceEvidence)
        {
            output.AppendLine("The comparison is provisional because some preference details are still unknown.");
        }

        var hasTimetable = options.Any(candidate => candidate.Conditions.Any(condition => !condition.TemporalWitness.IsDefaultOrEmpty));
        var hasPrices = options.Any(candidate => candidate.Highlights.Any(highlight => highlight.Value is ObjectiveMoneyValue));

        if (hasTimetable)
        {
            output.AppendLine("These times and costs come from the supplied information, not live checks. Verify transport departures and opening hours before leaving; no booking has been made.");
        }
        else if (hasPrices)
        {
            output.AppendLine("Prices are based on supplied information, not live checks. Verify current prices and availability before deciding.");
        }

        return output.ToString().TrimEnd();
    }

    private static void AppendOpening(
        StringBuilder output,
        ObjectiveEvaluation evaluation,
        IReadOnlyList<CandidateAssessment> options)
    {
        var names = options.Select(candidate => candidate.Candidate.Value).ToArray();

        switch (evaluation.Recommendation.Status)
        {
            case ObjectiveRecommendationStatus.UniquePreferenceLeader:
                output.Append("I'd recommend ").Append(names[0])
                    .AppendLine(": it is the strongest match for your stated preferences.");
                break;

            case ObjectiveRecommendationStatus.OnlyEligibleCandidate:
                output.Append(names[0])
                    .AppendLine(" is the only option currently confirmed to meet your requirements.");
                break;

            case ObjectiveRecommendationStatus.MultipleNonDominatedCandidates:
                output.Append("You have ")
                    .Append(options.Count switch
                    {
                        2 => "two",
                        3 => "three",
                        _ => options.Count.ToString(CultureInfo.InvariantCulture)
                    })
                    .Append(" good options: ")
                    .Append(JoinNames(names))
                    .Append(". ");

                var sharedRequirements = GetSharedSatisfiedRequirements(options);
                var sharedPreferences = GetSharedSatisfiedPreferences(options);

                if (sharedRequirements.Length > 0)
                {
                    output.Append(options.Count == 2 ? "Both " : "All options ")
                        .Append(JoinNames(sharedRequirements))
                        .Append('.');
                }

                if (sharedPreferences.Length > 0)
                {
                    output.Append(' ')
                        .Append(options.Count == 2 ? "They also match your stated preferences for " : "They also match your stated preferences for ")
                        .Append(JoinNames(sharedPreferences))
                        .Append('.');
                }

                if (sharedRequirements.Length == 0 && sharedPreferences.Length == 0)
                {
                    output.Append("They meet the assessed hard requirements, but have different strengths.");
                }

                output.AppendLine();
                break;
        }

        if (options.Count > 1)
        {
            var cheaperAndShorter = FindCheaperAndShorterOption(options);
            if (cheaperAndShorter is not null)
            {
                output.Append("If you'd like the lower-cost, shorter outing, I'd lean toward ")
                    .Append(cheaperAndShorter.Candidate.Value)
                    .AppendLine(".");
            }
        }
    }

    private static void AppendOptions(
        StringBuilder output,
        IReadOnlyList<CandidateAssessment> options)
    {
        output.AppendLine();
        output.AppendLine("Your options:");

        foreach (var candidate in options)
        {
            output.Append("- ").Append(candidate.Candidate.Value);

            var details = GetCandidateDetails(candidate);
            if (details.Length > 0)
            {
                output.Append(" — ").Append(string.Join("; ", details));
            }

            output.AppendLine(".");

            var schedule = FormatSchedule(candidate);
            if (schedule is not null)
            {
                output.Append("  ").AppendLine(schedule);
            }
        }
    }

    private static string[] GetCandidateDetails(CandidateAssessment candidate)
    {
        var details = new List<string>();

        var transportCost = FindMoneyHighlight(candidate, "return-transport-cost");
        var entryCost = FindMoneyHighlight(candidate, "entry-cost");
        if (transportCost is not null)
        {
            details.Add($"{FormatMoney(transportCost)} return transport");
        }

        if (entryCost is not null)
        {
            details.Add(entryCost.Amount == 0m ? "free entry" : $"{FormatMoney(entryCost)} entry");
        }

        var activity = candidate.Highlights
            .FirstOrDefault(highlight => string.Equals(
                highlight.Name.Value,
                "main-activity",
                StringComparison.OrdinalIgnoreCase))
            ?.Value as ObjectiveTextValue;

        if (activity is not null && !string.IsNullOrWhiteSpace(activity.Value))
        {
            details.Add(char.ToLowerInvariant(activity.Value[0]) + activity.Value[1..]);
        }

        return details.ToArray();
    }

    private static string? FormatSchedule(CandidateAssessment candidate)
    {
        var stages = candidate.Conditions
            .Where(condition => string.Equals(condition.ConditionName, "time-window", StringComparison.OrdinalIgnoreCase))
            .SelectMany(condition => condition.TemporalWitness)
            .OrderBy(stage => stage.Index)
            .ToArray();

        if (stages.Length == 0)
        {
            return null;
        }

        var mode = candidate.Conditions
            .SelectMany(condition => condition.Evidence)
            .FirstOrDefault(attribute => string.Equals(
                attribute.Name.Value,
                "transport-mode",
                StringComparison.OrdinalIgnoreCase))
            ?.Value as ObjectiveTextValue;
        var transportLabel = string.IsNullOrWhiteSpace(mode?.Value)
            ? "Travel"
            : string.Equals(mode.Value, "walking", StringComparison.OrdinalIgnoreCase)
                ? "Walk"
                : char.ToUpperInvariant(mode.Value[0]) + mode.Value[1..];

        if (stages.Length >= 3)
        {
            var activity = candidate.Highlights
                .FirstOrDefault(highlight => string.Equals(
                    highlight.Name.Value,
                    "main-activity",
                    StringComparison.OrdinalIgnoreCase))
                ?.Value as ObjectiveTextValue;
            var activityLabel = activity?.Value ?? "Activity";

            return $"{transportLabel} {stages[0].Start:HH:mm}–{stages[0].End:HH:mm}; {activityLabel} {stages[1].Start:HH:mm}–{stages[1].End:HH:mm}; return {transportLabel.ToLowerInvariant()} {stages[2].Start:HH:mm}–{stages[2].End:HH:mm}.";
        }

        return string.Join("; ", stages.Select(stage =>
            $"{stage.Start:HH:mm}–{stage.End:HH:mm}"));
    }

    private static void AppendPreferenceTradeOff(
        StringBuilder output,
        ObjectiveEvaluation evaluation,
        IReadOnlyList<CandidateAssessment> options)
    {
        var names = options.Select(candidate => candidate.Candidate).ToHashSet();
        foreach (var tradeOff in evaluation.PreferenceTradeOffs.Where(tradeOff =>
            names.Contains(tradeOff.CandidateWithPositiveEvidence) &&
            names.Contains(tradeOff.CandidateWithContraryEvidence)))
        {
            output.Append(tradeOff.CandidateWithPositiveEvidence.Value)
                .Append(" has stronger evidence for ")
                .Append(tradeOff.Preference)
                .Append(" than ")
                .Append(tradeOff.CandidateWithContraryEvidence.Value)
                .AppendLine(".");
        }
    }

    private static void AppendPreferenceMismatches(
        StringBuilder output,
        ObjectiveEvaluation evaluation,
        IReadOnlyList<CandidateAssessment> options)
    {
        var tradeOffs = evaluation.PreferenceTradeOffs
            .SelectMany(tradeOff => new[]
            {
                (tradeOff.CandidateWithPositiveEvidence, tradeOff.Preference),
                (tradeOff.CandidateWithContraryEvidence, tradeOff.Preference)
            })
            .ToHashSet();

        foreach (var candidate in options)
        {
            var mismatches = candidate.Conditions
                .Where(condition => condition.Role == ObjectiveConditionRole.Preference)
                .SelectMany(condition => condition.Preferences)
                .Where(preference =>
                    preference.Status is ObjectiveConditionAssessmentStatus.Violated or ObjectiveConditionAssessmentStatus.PartiallySatisfied)
                .Where(preference => !tradeOffs.Contains((candidate.Candidate, preference.Preference)))
                .Select(preference => preference.Preference)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (var mismatch in mismatches)
            {
                output.Append(candidate.Candidate.Value)
                    .Append(" doesn't match your preference for ")
                    .Append(mismatch)
                    .AppendLine(".");
            }
        }
    }

    private static void AppendOptionalCandidates(
        StringBuilder output,
        ObjectiveEvaluation evaluation,
        IReadOnlyList<CandidateAssessment> options)
    {
        var optionNames = options.Select(candidate => candidate.Candidate).ToHashSet();
        var requests = evaluation.EvidenceRequests
            .Where(request => !optionNames.Contains(request.Candidate))
            .GroupBy(request => request.Candidate)
            .ToArray();

        foreach (var requestGroup in requests)
        {
            var request = requestGroup.First();
            output.Append(request.Candidate.Value)
                .Append(" might also be an option, but its ")
                .Append(FormatAttributeName(request.AttributeName))
                .AppendLine(" is still unknown.");
        }
    }

    private static void AppendNoRecommendation(
        StringBuilder output,
        ObjectiveEvaluation evaluation)
    {
        var unresolved = evaluation.Candidates
            .Where(candidate => candidate.Feasibility == CandidateFeasibility.Unresolved)
            .Select(candidate => candidate.Candidate.Value)
            .ToArray();

        if (unresolved.Length > 0)
        {
            output.Append("I can't recommend an option yet. I still need information about ")
                .Append(JoinNames(unresolved))
                .AppendLine(" to check whether they meet your requirements.");
        }
        else
        {
            output.AppendLine("None of the supplied options currently meet all your requirements.");
            var blockers = evaluation.Candidates
                .SelectMany(candidate => candidate.Conditions
                    .Where(condition =>
                        condition.Role == ObjectiveConditionRole.HardConstraint &&
                        condition.Status == ObjectiveConditionAssessmentStatus.Violated)
                    .Select(condition => FormatAttributeName(condition.ConditionName)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (blockers.Length > 0)
            {
                output.Append("The options are blocked by: ")
                    .Append(JoinNames(blockers))
                    .AppendLine(".");
            }
        }

        if (!evaluation.EvidenceRequests.IsDefaultOrEmpty)
        {
            output.AppendLine(evaluation.EvidenceRequests[0].Prompt);
        }
    }

    private static string[] GetSharedSatisfiedRequirements(IReadOnlyList<CandidateAssessment> options)
    {
        var first = options[0].Conditions
            .Where(condition =>
                condition.Role == ObjectiveConditionRole.HardConstraint &&
                condition.Status == ObjectiveConditionAssessmentStatus.Satisfied)
            .Select(condition => condition.ConditionName)
            .ToArray();

        return first
            .Where(name => options.Skip(1).All(candidate => candidate.Conditions.Any(condition =>
                condition.Role == ObjectiveConditionRole.HardConstraint &&
                condition.ConditionName.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                condition.Status == ObjectiveConditionAssessmentStatus.Satisfied)))
            .Select(name => name.ToLowerInvariant() switch
            {
                "spending-limit" => "fit your budget",
                "transport" => "meet your transport requirements",
                "origin" => "use your chosen starting point",
                "time-window" => "fit your time window",
                _ => $"meet your {FormatAttributeName(name)} requirement"
            })
            .ToArray();
    }

    private static string[] GetSharedSatisfiedPreferences(IReadOnlyList<CandidateAssessment> options)
    {
        var preferences = options
            .Select(candidate => candidate.Conditions
                .Where(condition => condition.Role == ObjectiveConditionRole.Preference)
                .SelectMany(condition => condition.Preferences)
                .Where(preference => preference.Status == ObjectiveConditionAssessmentStatus.Satisfied)
                .Select(preference => preference.Preference)
                .ToHashSet(StringComparer.OrdinalIgnoreCase))
            .ToArray();

        return preferences.Length == 0
            ? []
            : preferences[0]
                .Where(preference => preferences.Skip(1).All(candidatePreferences => candidatePreferences.Contains(preference)))
                .ToArray();
    }

    private static CandidateAssessment? FindCheaperAndShorterOption(IReadOnlyList<CandidateAssessment> options)
    {
        var comparisons = options
            .Select(candidate => (
                Candidate: candidate,
                Cost: GetComparableCost(candidate),
                Duration: GetScheduledDuration(candidate)))
            .ToArray();

        if (comparisons.Any(item => item.Cost is null || item.Duration is null))
        {
            return null;
        }

        var cheapest = comparisons.Min(item => item.Cost!.Value);
        var shortest = comparisons.Min(item => item.Duration!.Value);
        var best = comparisons
            .Where(item => item.Cost == cheapest && item.Duration == shortest)
            .ToArray();

        return best.Length == 1 ? best[0].Candidate : null;
    }

    private static decimal? GetComparableCost(CandidateAssessment candidate)
    {
        var money = candidate.Highlights
            .Select(highlight => highlight.Value)
            .OfType<ObjectiveMoneyValue>()
            .ToArray();

        if (money.Length == 0 || money.Select(value => value.CurrencySymbol).Distinct(StringComparer.Ordinal).Count() != 1)
        {
            return null;
        }

        return money.Sum(value => value.Amount);
    }

    private static int? GetScheduledDuration(CandidateAssessment candidate)
    {
        var stages = candidate.Conditions
            .SelectMany(condition => condition.TemporalWitness)
            .OrderBy(stage => stage.Index)
            .ToArray();

        if (stages.Length == 0)
        {
            return null;
        }

        return (int)(stages[^1].End - stages[0].Start).TotalMinutes;
    }

    private static ObjectiveMoneyValue? FindMoneyHighlight(CandidateAssessment candidate, string name) =>
        candidate.Highlights
            .FirstOrDefault(highlight => string.Equals(
                highlight.Name.Value,
                name,
                StringComparison.OrdinalIgnoreCase))
            ?.Value as ObjectiveMoneyValue;

    private static string FormatMoney(ObjectiveMoneyValue money) =>
        $"{money.CurrencySymbol}{money.Amount.ToString("0.##", CultureInfo.InvariantCulture)}";

    private static string FormatAttributeName(string name) =>
        string.Join(' ', name.Split('-', StringSplitOptions.RemoveEmptyEntries));

    private static string JoinNames(IReadOnlyList<string> names) =>
        names.Count switch
        {
            0 => string.Empty,
            1 => names[0],
            2 => $"{names[0]} and {names[1]}",
            _ => $"{string.Join(", ", names.Take(names.Count - 1))}, and {names[^1]}"
        };
}
