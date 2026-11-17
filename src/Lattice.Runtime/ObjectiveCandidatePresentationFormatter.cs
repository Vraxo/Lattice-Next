using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

namespace Lattice.Runtime;

internal static class ObjectiveCandidatePresentationFormatter
{
        internal static string[] GetCandidateDetails(CandidateAssessment candidate)
        {
            var details = new List<string>();
    
            var transportCost = FindMoneyHighlight(candidate, "return-transport-cost");
            var entryCost = FindMoneyHighlight(candidate, "entry-cost");
            var totalCost = FindMoneyHighlight(candidate, "total-cost");
            if (transportCost is not null)
            {
                details.Add($"{ObjectiveResponseFormatter.FormatMoney(transportCost)} return transport");
            }
    
            if (entryCost is not null)
            {
                details.Add(entryCost.Amount == 0m ? "free entry" : $"{ObjectiveResponseFormatter.FormatMoney(entryCost)} entry");
            }
    
            if (totalCost is not null && transportCost is null && entryCost is null)
            {
                details.Add($"total cost {ObjectiveResponseFormatter.FormatMoney(totalCost)}");
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
    
        internal static string? FormatSchedule(CandidateAssessment candidate)
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
    
        internal static ObjectiveMoneyValue? FindMoneyHighlight(CandidateAssessment candidate, string name)
        {
            var highlight = candidate.Highlights
                .FirstOrDefault(attribute => string.Equals(
                    attribute.Name.Value,
                    name,
                    StringComparison.OrdinalIgnoreCase));
    
            if (highlight?.Value is ObjectiveMoneyValue highlightedMoney)
            {
                return highlightedMoney;
            }
    
            return candidate.Conditions
                .SelectMany(condition => condition.Evidence)
                .FirstOrDefault(attribute => string.Equals(
                    attribute.Name.Value,
                    name,
                    StringComparison.OrdinalIgnoreCase))
                ?.Value as ObjectiveMoneyValue;
        }
}
