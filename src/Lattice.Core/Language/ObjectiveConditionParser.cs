using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class ObjectiveConditionParser
{
    private static readonly Regex BudgetAmountPattern = new(
        @"(?:\bbudget\b[^.!?]*?|\b(?:I can spend up to|I have|I can afford up to|I don't want to spend more than|my maximum spend is)\s*)(?<symbol>[$€£])\s*(?<amount>\d[\d,]*(?:\.\d{1,2})?)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex LimitedBudgetPattern = new(
        @"\blimited budget\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex NoCarPattern = new(
        @"\b(?:no car|I don't have (?:access to )?a car|I do not have (?:access to )?a car|I won't have (?:access to )?a car|I will not have (?:access to )?a car|I'm without a car|I am without a car|I'm car-free|I am car-free)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex StartingLocationPattern = new(
        @"\b(?:I'm starting at|my starting point is|my starting location is|I'm coming from|I'm starting from|I'll start from|I'll be leaving from|I'm setting off from)\s+(?<location>[^,.!?]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex PreferencePattern = new(
        @"\b(?:I would prefer|I prefer|I care about|I want(?!\s+to\b)|I would like(?!\s+to\b)|I'd (?:like|prefer)(?!\s+to\b))\s+(?<value>[^.!?]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex TimeWindowPattern = new(
        @"\b(?:(?:I can )?(?:leave at|head out at|set off at) (?<start>\d{1,2}:\d{2}) and (?:be back|return) by (?<end>\d{1,2}:\d{2})|(?:(?:I can )?(?:be back|return) by|I need to be back by|I need to return by) (?<reverseEnd>\d{1,2}:\d{2}) and (?:(?:I )?can )?(?:leave at|head out at|set off at) (?<reverseStart>\d{1,2}:\d{2}))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static ImmutableArray<ObjectiveCondition> Parse(string source)
    {
        var conditions = new List<(int Index, ObjectiveCondition Condition)>();
        var budgetAmount = BudgetAmountPattern.Match(source);

        if (budgetAmount.Success &&
            decimal.TryParse(
                budgetAmount.Groups["amount"].Value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var amount))
        {
            conditions.Add((budgetAmount.Index, new ObjectiveCondition(
                "spending-limit",
                ObjectiveConditionRole.HardConstraint,
                ObjectiveConditionOperator.AtMost,
                new ObjectiveMoneyValue(amount, budgetAmount.Groups["symbol"].Value),
                budgetAmount.Value)
            {
                TargetAttribute = "total-cost",
                ExpectedValueKind = ObjectiveConditionValueKind.Money,
                MissingValuePrompt = "What is your spending limit?"
            }));
        }
        else
        {
            var limitedBudget = LimitedBudgetPattern.Match(source);

            if (limitedBudget.Success)
            {
                conditions.Add((limitedBudget.Index, new ObjectiveCondition(
                    "spending-limit",
                    ObjectiveConditionRole.HardConstraint,
                    ObjectiveConditionOperator.AtMost,
                    null,
                    limitedBudget.Value)
                {
                    TargetAttribute = "total-cost",
                    ExpectedValueKind = ObjectiveConditionValueKind.Money,
                    MissingValuePrompt = "What is your spending limit?"
                }));
            }
        }

        var noCar = NoCarPattern.Match(source);

        if (noCar.Success)
        {
            conditions.Add((noCar.Index, new ObjectiveCondition(
                "transport",
                ObjectiveConditionRole.HardConstraint,
                ObjectiveConditionOperator.Excludes,
                new ObjectiveTextValue("car"),
                noCar.Value)
            {
                TargetAttribute = "transport-mode"
            }));
        }

        var startingLocation = StartingLocationPattern.Match(source);

        if (startingLocation.Success)
        {
            conditions.Add((startingLocation.Index, new ObjectiveCondition(
                "origin",
                ObjectiveConditionRole.HardConstraint,
                ObjectiveConditionOperator.Equals,
                new ObjectiveTextValue(startingLocation.Groups["location"].Value.Trim()),
                startingLocation.Value)
            {
                TargetAttribute = "origin"
            }));
        }

        var timeWindow = TimeWindowPattern.Match(source);
        var startText = timeWindow.Groups["start"].Success
            ? timeWindow.Groups["start"].Value
            : timeWindow.Groups["reverseStart"].Value;
        var endText = timeWindow.Groups["end"].Success
            ? timeWindow.Groups["end"].Value
            : timeWindow.Groups["reverseEnd"].Value;

        if (timeWindow.Success &&
            TimeOnly.TryParse(startText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var start) &&
            TimeOnly.TryParse(endText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
        {
            conditions.Add((timeWindow.Index, new ObjectiveCondition(
                "time-window",
                ObjectiveConditionRole.HardConstraint,
                ObjectiveConditionOperator.CanFit,
                new ObjectiveTimeWindowValue(start, end),
                timeWindow.Value)
            {
                TargetAttribute = "temporal-plan"
            }));
        }

        var preference = PreferencePattern.Match(source);

        if (preference.Success)
        {
            var preferenceText = preference.Groups["value"].Value.Trim();

            if (preferenceText.StartsWith("somewhere ", StringComparison.OrdinalIgnoreCase))
            {
                preferenceText = preferenceText["somewhere ".Length..];
            }

            preferenceText = Regex.Replace(
                preferenceText,
                @",?\s+\band\b\s+",
                ",",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            var preferenceValues = preferenceText
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim().ToLowerInvariant())
                .Where(value => value.Length > 0)
                .ToImmutableArray();

            if (!preferenceValues.IsDefaultOrEmpty)
            {
                conditions.Add((preference.Index, new ObjectiveCondition(
                    "preference",
                    ObjectiveConditionRole.Preference,
                    ObjectiveConditionOperator.Desires,
                    new ObjectiveTextSetValue(preferenceValues),
                    preference.Value)
                {
                    TargetAttribute = "tags"
                }));
            }
        }

        return conditions
            .OrderBy(item => item.Index)
            .Select(item => item.Condition)
            .ToImmutableArray();
    }
}
