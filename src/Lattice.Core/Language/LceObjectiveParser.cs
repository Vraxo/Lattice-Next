using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class LceObjectiveParser
{
    private static readonly Regex ObjectiveRequestPattern = new(
        @"(?:^|[.!?]\s*)(?:Help me\s+|Can you help me\s+|Please\s+|Let's\s+|I'd like to\s+|I would like to\s+)(?<goal>[^.!?]+)[.!?]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ObjectiveCandidateMoneyPattern = new(
        @"^(?<candidate>[A-Z][A-Za-z0-9-]*(?:\s+[A-Za-z0-9][A-Za-z0-9-]*)*)\s+costs\s+(?<symbol>[$€£])\s*(?<amount>\d[\d,]*(?:\.\d{1,2})?)\.$",
        RegexOptions.CultureInvariant);

    private static readonly Regex ObjectiveCandidateTextPattern = new(
        @"^(?<candidate>[A-Z][A-Za-z0-9-]*(?:\s+[A-Za-z0-9][A-Za-z0-9-]*)*)['’]s\s+(?<attribute>[A-Za-z][A-Za-z -]*)\s+is\s+(?<value>[^.!?]+)[.!?]$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ObjectiveMoneyResponsePattern = new(
        @"^(?<symbol>[$€£])\s*(?<amount>\d[\d,]*(?:\.\d{1,2})?)(?:\s+total)?\.$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ObjectiveMoneySpendResponsePattern = new(
        @"^I can spend up to (?<symbol>[$€£])\s*(?<amount>\d[\d,]*(?:\.\d{1,2})?)(?:\s+total)?\.$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex BringLunchPattern = new(
        @"^(?:I'll bring lunch|Lunch is covered|I have lunch with me|I'll pack my own lunch|I've got lunch sorted|I already have lunch|I'm bringing lunch|I'm bringing my own lunch|I'll have a packed lunch|I won't need to buy lunch)\.$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex NoBusPattern = new(
        @"\bI don't want to take a bus\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex WalkingOnlyPattern = new(
        @"^Walking only\.$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ExplicitCorrectionPattern = new(
        @"^(?:Actually,|Correction:|To clarify,|I meant\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ObjectivePreferenceRevisionPattern = new(
        @"^(?:(?:Actually,|Correction:|To clarify,)\s*)?(?:I would prefer|I prefer|I care about|I'd prefer)\s+[^.!?]+[.!?]$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ObjectiveBudgetRevisionPattern = new(
        @"^(?:(?:Actually,|Correction:|To clarify,)\s*|I meant\s+)?(?:(?:my|the)\s+)?budget\b[^.!?]*[$€£]\s*\d[\d,]*(?:\.\d{1,2})?[^.!?]*[.!?]$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ObjectivePreferenceAdditionPattern = new(
        @"^(?:Also,\s*(?:I care about|I prefer)|I also care about|I also prefer)\s+(?<value>[^.!?]+)[.!?]$|^Add\s+(?<add>.+?)\s+to my preferences[.!?]$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ObjectivePreferenceRemovalPattern = new(
        @"^Remove\s+(?<value>.+?)\s+from my preferences[.!?]$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);


    internal static bool TryParseObjectiveCandidateInformation(
        string source,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        var moneyMatch = ObjectiveCandidateMoneyPattern.Match(source);

        if (moneyMatch.Success &&
            decimal.TryParse(
                moneyMatch.Groups["amount"].Value,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out var amount))
        {
            utterance = new SemanticUtterance(
                UtteranceKind.ObjectiveCandidateInformation,
                new ObjectiveCandidateInformationFormula(
                    new SemanticIdentifier(moneyMatch.Groups["candidate"].Value.ToLowerInvariant()),
                    new SemanticIdentifier("total-cost"),
                    new ObjectiveMoneyValue(amount, moneyMatch.Groups["symbol"].Value)),
                sourceText.Trim());
            return true;
        }

        var textMatch = ObjectiveCandidateTextPattern.Match(source);

        if (!textMatch.Success)
        {
            utterance = null;
            return false;
        }

        var attributeName = Regex.Replace(
            textMatch.Groups["attribute"].Value.Trim(),
            @"\s+",
            "-").ToLowerInvariant();

        utterance = new SemanticUtterance(
            UtteranceKind.ObjectiveCandidateInformation,
            new ObjectiveCandidateInformationFormula(
                new SemanticIdentifier(textMatch.Groups["candidate"].Value.ToLowerInvariant()),
                new SemanticIdentifier(attributeName),
                new ObjectiveTextValue(textMatch.Groups["value"].Value.Trim())),
            sourceText.Trim());
        return true;
    }

    internal static bool TryParseObjectiveInformation(
        string source,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        var match = ObjectiveMoneyResponsePattern.Match(source);

        if (!match.Success)
        {
            match = ObjectiveMoneySpendResponsePattern.Match(source);
        }

        if (!match.Success ||
            !decimal.TryParse(
                match.Groups["amount"].Value,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out var amount))
        {
            utterance = null;
            return false;
        }

        utterance = new SemanticUtterance(
            UtteranceKind.ObjectiveInformation,
            new ObjectiveInformationFormula(new ObjectiveMoneyValue(
                amount,
                match.Groups["symbol"].Value)),
            sourceText.Trim());
        return true;
    }

    internal static bool TryParseObjectiveConditionUpdates(
        string source,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        var conditions = ImmutableArray.CreateBuilder<ObjectiveCondition>();
        var preferenceEdits = ImmutableArray.CreateBuilder<ObjectivePreferenceEdit>();
        var isPreferenceRevision = ObjectivePreferenceRevisionPattern.IsMatch(source);
        var isBudgetRevision = ObjectiveBudgetRevisionPattern.IsMatch(source);
        var preferenceAddition = ObjectivePreferenceAdditionPattern.Match(source);
        var preferenceRemoval = ObjectivePreferenceRemovalPattern.Match(source);

        if (preferenceAddition.Success || preferenceRemoval.Success)
        {
            var match = preferenceAddition.Success ? preferenceAddition : preferenceRemoval;
            var value = match.Groups["value"].Success
                ? match.Groups["value"].Value
                : match.Groups["add"].Value;
            var parsedPreferences = ObjectiveConditionParser.Parse($"I care about {value}.");
            var values = parsedPreferences
                .Where(condition => condition.Role == ObjectiveConditionRole.Preference)
                .OfType<ObjectiveCondition>()
                .SelectMany(condition => condition.Value is ObjectiveTextSetValue set
                    ? set.Values
                    : ImmutableArray<string>.Empty)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToImmutableArray();

            if (!values.IsDefaultOrEmpty)
            {
                preferenceEdits.Add(new ObjectivePreferenceEdit(
                    preferenceAddition.Success
                        ? ObjectivePreferenceEditKind.Add
                        : ObjectivePreferenceEditKind.Remove,
                    values,
                    sourceText));
            }
        }

        if (isPreferenceRevision || isBudgetRevision)
        {
            conditions.AddRange(ObjectiveConditionParser.Parse(source).Where(condition =>
                (isPreferenceRevision && condition.Role == ObjectiveConditionRole.Preference) ||
                (isBudgetRevision && condition.Name == "spending-limit")));
        }

        if (BringLunchPattern.IsMatch(source))
        {
            conditions.Add(new ObjectiveCondition(
                "meal-plan",
                ObjectiveConditionRole.HardConstraint,
                ObjectiveConditionOperator.Equals,
                new ObjectiveTextValue("bring-lunch"),
                sourceText)
            {
                // The user's meal plan is context for the cost calculation, not a candidate property.
                TargetAttribute = null
            });
        }

        conditions.AddRange(ObjectiveConditionParser.Parse(source).Where(condition =>
            condition.Name == "origin"));

        conditions.AddRange(ObjectiveConditionParser.Parse(source).Where(condition =>
            condition.Name == "time-window"));

        if (NoBusPattern.IsMatch(source))
        {
            conditions.Add(new ObjectiveCondition(
                "transport-bus",
                ObjectiveConditionRole.HardConstraint,
                ObjectiveConditionOperator.Excludes,
                new ObjectiveTextValue("bus"),
                sourceText)
            {
                TargetAttribute = "transport-mode"
            });
        }

        if (WalkingOnlyPattern.IsMatch(source))
        {
            conditions.Add(new ObjectiveCondition(
                "transport-mode",
                ObjectiveConditionRole.HardConstraint,
                ObjectiveConditionOperator.Equals,
                new ObjectiveTextValue("walking"),
                sourceText)
            {
                TargetAttribute = "transport-mode"
            });
        }

        if (conditions.Count == 0 && preferenceEdits.Count == 0)
        {
            utterance = null;
            return false;
        }

        utterance = new SemanticUtterance(
            UtteranceKind.ObjectiveConditionUpdate,
            new ObjectiveConditionUpdateFormula(conditions.ToImmutable())
            {
                PreferenceEdits = preferenceEdits.ToImmutable(),
                IsExplicitCorrection = ExplicitCorrectionPattern.IsMatch(source)
            },
            sourceText.Trim());
        return true;
    }

    internal static bool TryParseObjective(
        string source,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        var match = ObjectiveRequestPattern.Match(source);

        if (!match.Success)
        {
            utterance = null;
            return false;
        }

        var goal = match.Groups["goal"].Value.Trim();

        if (goal.Length == 0)
        {
            utterance = null;
            return false;
        }

        utterance = new SemanticUtterance(
            UtteranceKind.Objective,
            new ObjectiveFormula(goal)
            {
                Conditions = ObjectiveConditionParser.Parse(sourceText)
            },
            sourceText.Trim());
        return true;
    }
}
