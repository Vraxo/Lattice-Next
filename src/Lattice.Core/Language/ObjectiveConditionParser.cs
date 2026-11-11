using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class ObjectiveConditionParser
{
    private static readonly Regex BudgetAmountPattern = new(
        @"\bbudget\b[^.!?]*?(?<symbol>[$€£])\s*(?<amount>\d[\d,]*(?:\.\d{1,2})?)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex LimitedBudgetPattern = new(
        @"\blimited budget\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex NoCarPattern = new(
        @"\bno car\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex PreferencePattern = new(
        @"\b(?:I would prefer|I prefer|I care about)\s+(?<value>[^.!?]+)",
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
