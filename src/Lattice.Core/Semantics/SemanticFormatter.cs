namespace Lattice.Core.Semantics;

public static class SemanticFormatter
{
    public static string Format(SemanticUtterance utterance)
    {
        ArgumentNullException.ThrowIfNull(utterance);

        var meaning = Format(utterance.Meaning);

        return utterance.Kind switch
        {
            UtteranceKind.Assertion => meaning,
            UtteranceKind.Question => $"ask({meaning})",
            UtteranceKind.Objective => meaning,
            UtteranceKind.ObjectiveInformation => meaning,
            UtteranceKind.ObjectiveConditionUpdate => meaning,
            _ => throw new ArgumentOutOfRangeException(nameof(utterance), utterance.Kind, "Unsupported utterance kind.")
        };
    }

    private static string Format(SemanticFormula formula) =>
        formula switch
        {
            ObjectiveFormula objective => FormatObjective(objective),
            ObjectiveInformationFormula information => FormatObjectiveInformation(information),
            ObjectiveConditionUpdateFormula update => FormatObjectiveConditionUpdate(update),
            PredicateApplication predicate => FormatPredicate(predicate),
            Negation negation => $"not({Format(negation.Formula)})",
            Implication implication => $"implies({Format(implication.Premise)}, {Format(implication.Conclusion)})",
            Conjunction conjunction => $"and({string.Join(", ", conjunction.Conjuncts.Select(Format))})",
            ExistentialQuantification existential => $"exists({Format(existential.Variable)}, {Format(existential.Body)})",
            UniversalQuantification universal => $"forall({Format(universal.Variable)}, {Format(universal.Body)})",
            _ => throw new ArgumentOutOfRangeException(nameof(formula), formula, "Unsupported semantic formula.")
        };

    private static string FormatObjectiveConditionUpdate(ObjectiveConditionUpdateFormula update)
    {
        var conditions = update.Conditions.Select(FormatObjectiveCondition);
        var edits = update.PreferenceEdits.Select(edit =>
            $"{(edit.Kind == ObjectivePreferenceEditKind.Add ? "add" : "remove")}-preference({string.Join("+", edit.Values)})");

        return string.Join(", ", conditions.Concat(edits));
    }

    private static string FormatObjectiveCondition(ObjectiveCondition condition)
    {
        var value = condition.Value switch
        {
            ObjectiveMoneyValue money => $"{money.CurrencySymbol}{money.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            ObjectiveTextValue text => text.Value,
            ObjectiveTextSetValue set => string.Join("+", set.Values),
            ObjectiveTimeWindowValue time => $"{time.Start:HH:mm}-{time.End:HH:mm}",
            ObjectiveDurationValue duration => $"{duration.Minutes}m",
            ObjectiveTimeSetValue times => string.Join("+", times.Times.Select(time => time.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture))),
            null => "unknown",
            _ => "unsupported"
        };

        return $"{condition.Role.ToString().ToLowerInvariant()}({condition.Name}, {condition.Operator.ToString().ToLowerInvariant()}, {value})";
    }

    private static string FormatObjectiveInformation(ObjectiveInformationFormula information) =>
        information.Value switch
        {
            ObjectiveMoneyValue money => $"money({money.CurrencySymbol}{money.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)})",
            ObjectiveTextValue text => $"text({text.Value})",
            ObjectiveTextSetValue set => $"text-set({string.Join("+", set.Values)})",
            ObjectiveTimeWindowValue time => $"time-window({time.Start:HH:mm}-{time.End:HH:mm})",
            ObjectiveDurationValue duration => $"duration({duration.Minutes}m)",
            ObjectiveTimeSetValue times => $"time-set({string.Join("+", times.Times.Select(time => time.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture)))})",
            _ => "information(unsupported)"
        };

    private static string FormatObjective(ObjectiveFormula objective)
    {
        if (objective.Conditions.IsDefaultOrEmpty)
        {
            return $"objective({objective.Goal})";
        }

        var conditions = string.Join(", ", objective.Conditions.Select(condition =>
        {
            var value = condition.Value switch
            {
                ObjectiveMoneyValue money => $"{money.CurrencySymbol}{money.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                ObjectiveTextValue text => text.Value,
                ObjectiveTextSetValue set => string.Join("+", set.Values),
                ObjectiveTimeWindowValue time => $"{time.Start:HH:mm}-{time.End:HH:mm}",
                ObjectiveDurationValue duration => $"{duration.Minutes}m",
                ObjectiveTimeSetValue times => string.Join("+", times.Times.Select(time => time.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture))),
                null => "unknown",
                _ => "unsupported"
            };

            return $"{condition.Role.ToString().ToLowerInvariant()}({condition.Name}, {condition.Operator.ToString().ToLowerInvariant()}, {value})";
        }));

        return $"objective({objective.Goal}; {conditions})";
    }

    private static string FormatPredicate(PredicateApplication predicate)
    {
        var arguments = string.Join(", ", predicate.Arguments.Select(Format));
        return $"{predicate.Predicate}({arguments})";
    }

    private static string Format(SemanticTerm term) =>
        term switch
        {
            IndividualTerm individual => individual.Identifier.Value,
            AnonymousIndividualTerm anonymous => $"anonymous({anonymous.Identity.Value})",
            DescriptiveIndividualTerm description => description.Identifier.Value,
            VariableTerm variable => variable.Identifier.Value,
            UnresolvedReferenceTerm reference => reference.Reference.Value,
            _ => throw new ArgumentOutOfRangeException(nameof(term), term, "Unsupported semantic term.")
        };
}
