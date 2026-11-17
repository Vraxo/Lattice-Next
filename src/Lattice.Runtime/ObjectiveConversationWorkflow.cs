using System.Collections.Immutable;
using System.Globalization;
using Lattice.Client;
using Lattice.Core.Intelligence;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

namespace Lattice.Runtime;

internal sealed class ObjectiveConversationWorkflow
{
    private readonly KnowledgeBase knowledgeBase;
    private readonly ObjectiveEvaluator objectiveEvaluator;
    private readonly Func<ObjectiveFormula?> getActiveObjective;
    private readonly Action<ObjectiveFormula?> setActiveObjective;
    private readonly Func<string?> getActiveObjectiveSource;
    private readonly Action<string?> setActiveObjectiveSource;
    private readonly Func<ObjectiveCondition?> getPendingCondition;
    private readonly Action<ObjectiveCondition?> setPendingCondition;

    public ObjectiveConversationWorkflow(
        KnowledgeBase knowledgeBase,
        ObjectiveEvaluator objectiveEvaluator,
        Func<ObjectiveFormula?> getActiveObjective,
        Action<ObjectiveFormula?> setActiveObjective,
        Func<string?> getActiveObjectiveSource,
        Action<string?> setActiveObjectiveSource,
        Func<ObjectiveCondition?> getPendingCondition,
        Action<ObjectiveCondition?> setPendingCondition)
    {
        this.knowledgeBase = knowledgeBase;
        this.objectiveEvaluator = objectiveEvaluator;
        this.getActiveObjective = getActiveObjective;
        this.setActiveObjective = setActiveObjective;
        this.getActiveObjectiveSource = getActiveObjectiveSource;
        this.setActiveObjectiveSource = setActiveObjectiveSource;
        this.getPendingCondition = getPendingCondition;
        this.setPendingCondition = setPendingCondition;
    }

    private ObjectiveFormula? ActiveObjective
    {
        get => getActiveObjective();
        set => setActiveObjective(value);
    }

    private string? ActiveObjectiveSource
    {
        get => getActiveObjectiveSource();
        set => setActiveObjectiveSource(value);
    }

    private ObjectiveCondition? PendingCondition
    {
        get => getPendingCondition();
        set => setPendingCondition(value);
    }

    public LatticeUtteranceResult ProcessCandidateInformation(
        SemanticUtterance parsedUtterance,
        ObjectiveCandidateInformationFormula information,
        bool deferObjectiveEvaluation)
    {
        var objective = ActiveObjective;

        if (objective is null)
        {
            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.ClarificationRequired,
                "There is no active objective requesting candidate information.",
                SemanticFormatter.Format(parsedUtterance));
        }

        var candidate = new SemanticIdentifier(information.Candidate.Value);
        var attribute = new SemanticIdentifier(information.Attribute.Value);
        var candidateEvidenceRequests = objectiveEvaluator
            .Evaluate(objective, knowledgeBase)
            .EvidenceRequests
            .Where(request =>
                string.Equals(request.AttributeName, attribute.Value, StringComparison.OrdinalIgnoreCase))
            .ToImmutableArray();

        if (candidate.Value.Equals("it", StringComparison.OrdinalIgnoreCase) ||
            candidate.Value.Equals("that one", StringComparison.OrdinalIgnoreCase) ||
            candidate.Value.Equals("this one", StringComparison.OrdinalIgnoreCase))
        {
            var possibleCandidates = candidateEvidenceRequests
                .Select(request => request.Candidate)
                .Distinct()
                .ToImmutableArray();

            if (possibleCandidates.Length > 1)
            {
                var candidateNames = string.Join(
                    ", ",
                    possibleCandidates.Select(possibleCandidate => possibleCandidate.Value));

                return new LatticeUtteranceResult(
                    LatticeUtteranceResultKind.ClarificationRequired,
                    $"Which candidate do you mean? I still need the {attribute.Value} for {candidateNames}. No candidate information was changed.",
                    SemanticFormatter.Format(parsedUtterance));
            }

            if (possibleCandidates.Length == 1)
            {
                candidate = possibleCandidates[0];
            }
        }

        var matchingRequests = candidateEvidenceRequests
            .Where(request =>
                string.Equals(request.Candidate.Value, candidate.Value, StringComparison.OrdinalIgnoreCase))
            .ToImmutableArray();
        var existingAttribute = knowledgeBase.Attributes.FirstOrDefault(existing =>
            string.Equals(existing.Subject.Value, candidate.Value, StringComparison.OrdinalIgnoreCase) &&
            existing.Name == attribute);

        if (matchingRequests.Length > 1)
        {
            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.ClarificationRequired,
                "I have more than one outstanding request for that candidate attribute. Please clarify which value you mean. No candidate information was changed.",
                SemanticFormatter.Format(parsedUtterance));
        }

        if (matchingRequests.IsDefaultOrEmpty && existingAttribute is null)
        {
            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.ClarificationRequired,
                $"I don't currently have an outstanding request for the {attribute.Value} of {candidate.Value}. No candidate information was changed.",
                SemanticFormatter.Format(parsedUtterance));
        }

        var storedCandidate = matchingRequests.IsDefaultOrEmpty
            ? existingAttribute!.Subject
            : matchingRequests[0].Candidate;
        var requestedConditionName = matchingRequests.IsDefaultOrEmpty
            ? null
            : matchingRequests[0].ConditionName;
        var condition = objective.Conditions.FirstOrDefault(candidateCondition =>
            candidateCondition.Role == ObjectiveConditionRole.HardConstraint &&
            candidateCondition.TargetAttribute == attribute.Value &&
            (requestedConditionName is null || candidateCondition.Name == requestedConditionName));

        var hasCompatibleValue = condition is not null &&
            (condition.Operator switch
            {
                ObjectiveConditionOperator.AtMost =>
                    condition.Value is ObjectiveMoneyValue && information.Value is ObjectiveMoneyValue,
                ObjectiveConditionOperator.Excludes or ObjectiveConditionOperator.Equals =>
                    condition.Value is ObjectiveTextValue && information.Value is ObjectiveTextValue,
                _ => false
            });

        if (!hasCompatibleValue)
        {
            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.ClarificationRequired,
                $"The supplied value type does not match the requested attribute for {candidate.Value}. No candidate information was changed.",
                SemanticFormatter.Format(parsedUtterance));
        }

        if (!knowledgeBase.TryAddAttribute(
            new KnowledgeAttribute(
                storedCandidate,
                attribute,
                information.Value,
                parsedUtterance.SourceText,
                KnowledgeSourceKind.UserAssertion),
            out var error))
        {
            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.StatementRejected,
                error ?? "The candidate information could not be stored.",
                SemanticFormatter.Format(parsedUtterance));
        }

        var originalSource = ActiveObjectiveSource ?? objective.Goal;
        ActiveObjectiveSource = $"{originalSource}{Environment.NewLine}{parsedUtterance.SourceText}";

        var message = information.Value switch
        {
            ObjectiveMoneyValue money =>
                $"Recorded {storedCandidate.Value}'s total cost as {money.CurrencySymbol}{money.Amount.ToString(CultureInfo.InvariantCulture)}.",
            ObjectiveTextValue text =>
                $"Recorded {storedCandidate.Value}'s {attribute.Value} as {text.Value}.",
            _ => $"Recorded candidate evidence for {storedCandidate.Value}."
        };
        if (!deferObjectiveEvaluation)
        {
            message = AppendObjectiveEvaluation(message, objective);
        }

        return new LatticeUtteranceResult(
            LatticeUtteranceResultKind.ObjectiveInformationReceived,
            message,
            SemanticFormatter.Format(parsedUtterance));
    }

    public LatticeUtteranceResult ProcessInformation(
        SemanticUtterance parsedUtterance,
        ObjectiveInformationFormula information,
        bool deferObjectiveEvaluation)
    {
        var objective = ActiveObjective;

        if (objective is null)
        {
            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.ClarificationRequired,
                "There is no outstanding objective information request to answer.",
                SemanticFormatter.Format(parsedUtterance));
        }

        var pendingConditions = objective.Conditions
            .Where(condition =>
                condition.Role == ObjectiveConditionRole.HardConstraint &&
                condition.Value is null &&
                condition.ExpectedValueKind != ObjectiveConditionValueKind.None)
            .ToImmutableArray();
        var compatibleConditions = pendingConditions
            .Where(condition => IsExpectedValue(condition.ExpectedValueKind, information.Value))
            .ToImmutableArray();

        if (compatibleConditions.Length > 1)
        {
            var alternatives = string.Join(
                "; ",
                compatibleConditions.Select(condition =>
                    $"{condition.Name}: {condition.MissingValuePrompt ?? $"provide {condition.Name}"}"));

            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.ClarificationRequired,
                $"That value could answer more than one outstanding condition: {alternatives}. Please name the condition you are answering. No objective conditions were changed.",
                SemanticFormatter.Format(parsedUtterance));
        }

        var pendingCondition = compatibleConditions.Length == 1
            ? compatibleConditions[0]
            : PendingCondition;

        if (pendingCondition is null)
        {
            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.ClarificationRequired,
                "There is no outstanding objective information request to answer.",
                SemanticFormatter.Format(parsedUtterance));
        }

        if (!IsExpectedValue(pendingCondition.ExpectedValueKind, information.Value))
        {
            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.ClarificationRequired,
                pendingCondition.MissingValuePrompt ??
                    $"Please provide a value for {pendingCondition.Name}.",
                SemanticFormatter.Format(parsedUtterance));
        }

        var answeredCondition = pendingCondition with
        {
            Value = information.Value,
            ValueSourceText = parsedUtterance.SourceText
        };

        objective = objective with
        {
            Conditions = objective.Conditions
                .Select(condition => ReferenceEquals(condition, pendingCondition)
                    ? answeredCondition
                    : condition)
                .ToImmutableArray()
        };
        ActiveObjective = objective;
        var originalSource = ActiveObjectiveSource ?? parsedUtterance.SourceText;
        ActiveObjectiveSource = $"{originalSource}{Environment.NewLine}{parsedUtterance.SourceText}";
        PendingCondition = FindPendingCondition(objective);

        var message = $"Recorded {parsedUtterance.SourceText} for {answeredCondition.Name}.";

        if (PendingCondition is not null)
        {
            message = $"{message} {PendingCondition.MissingValuePrompt}";
        }
        else if (!deferObjectiveEvaluation)
        {
            message = AppendObjectiveEvaluation(message, objective);
        }

        return new LatticeUtteranceResult(
            LatticeUtteranceResultKind.ObjectiveInformationReceived,
            message,
            SemanticFormatter.Format(new SemanticUtterance(
                UtteranceKind.Objective,
                objective,
                ActiveObjectiveSource)));
    }

    public LatticeUtteranceResult ProcessConditionUpdates(
        SemanticUtterance parsedUtterance,
        ObjectiveConditionUpdateFormula update,
        bool deferObjectiveEvaluation)
    {
        var objective = ActiveObjective;

        if (objective is null)
        {
            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.ClarificationRequired,
                "There is no active objective to update.",
                SemanticFormatter.Format(parsedUtterance));
        }

        var conditions = objective.Conditions.ToBuilder();

        foreach (var edit in update.PreferenceEdits)
        {
            var preferenceIndex = -1;

            for (var index = 0; index < conditions.Count; index++)
            {
                if (conditions[index].Name == "preference" &&
                    conditions[index].Role == ObjectiveConditionRole.Preference &&
                    conditions[index].Value is ObjectiveTextSetValue)
                {
                    preferenceIndex = index;
                    break;
                }
            }

            if (edit.Kind == ObjectivePreferenceEditKind.Add)
            {
                if (preferenceIndex < 0)
                {
                    conditions.Add(new ObjectiveCondition(
                        "preference",
                        ObjectiveConditionRole.Preference,
                        ObjectiveConditionOperator.Desires,
                        new ObjectiveTextSetValue(edit.Values),
                        edit.SourceText)
                    {
                        TargetAttribute = "tags"
                    });
                }
                else
                {
                    var existing = conditions[preferenceIndex];
                    var values = ((ObjectiveTextSetValue)existing.Value!).Values
                        .Concat(edit.Values)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToImmutableArray();
                    conditions[preferenceIndex] = existing with
                    {
                        Value = new ObjectiveTextSetValue(values)
                    };
                }
            }
            else if (preferenceIndex >= 0)
            {
                var existing = conditions[preferenceIndex];
                var values = ((ObjectiveTextSetValue)existing.Value!).Values
                    .Where(value => !edit.Values.Contains(value, StringComparer.OrdinalIgnoreCase))
                    .ToImmutableArray();

                if (values.IsDefaultOrEmpty)
                {
                    conditions.RemoveAt(preferenceIndex);
                }
                else
                {
                    conditions[preferenceIndex] = existing with
                    {
                        Value = new ObjectiveTextSetValue(values)
                    };
                }
            }
        }

        foreach (var condition in update.Conditions)
        {
            if (condition.Name == "spending-limit" &&
                condition.Role == ObjectiveConditionRole.HardConstraint &&
                !update.IsExplicitCorrection)
            {
                var currentBudget = conditions.FirstOrDefault(existing =>
                    existing.Name == condition.Name &&
                    existing.Role == ObjectiveConditionRole.HardConstraint);

                if (currentBudget is not null &&
                    !Equals(currentBudget.Value, condition.Value))
                {
                    return new LatticeUtteranceResult(
                        LatticeUtteranceResultKind.ClarificationRequired,
                        "That conflicts with the spending limit already in this plan. Do you mean to change the budget? If so, state it as an explicit correction beginning with 'Actually,'. No objective conditions were changed.",
                        SemanticFormatter.Format(parsedUtterance));
                }
            }

            var existingIndex = -1;

            for (var index = 0; index < conditions.Count; index++)
            {
                if (conditions[index].Name == condition.Name)
                {
                    existingIndex = index;
                    break;
                }
            }

            if (existingIndex >= 0)
            {
                conditions[existingIndex] = condition;
            }
            else
            {
                conditions.Add(condition);
            }
        }

        objective = objective with
        {
            Conditions = conditions.ToImmutable()
        };
        ActiveObjective = objective;
        var originalSource = ActiveObjectiveSource ?? parsedUtterance.SourceText;
        ActiveObjectiveSource = $"{originalSource}{Environment.NewLine}{parsedUtterance.SourceText}";
        PendingCondition = FindPendingCondition(objective);

        var names = string.Join(", ", update.Conditions.Select(condition => condition.Name));
        var message = $"Updated objective conditions: {names}.";
        if (!deferObjectiveEvaluation)
        {
            message = AppendObjectiveEvaluation(message, objective);
        }

        return new LatticeUtteranceResult(
            LatticeUtteranceResultKind.ObjectiveInformationReceived,
            message,
            SemanticFormatter.Format(new SemanticUtterance(
                UtteranceKind.Objective,
                objective,
                ActiveObjectiveSource)));
    }

    public string AppendObjectiveEvaluation(
        string message,
        ObjectiveFormula objective,
        bool includeMessage = true)
    {
        var response = ObjectiveResponseFormatter.Format(
            objectiveEvaluator.Evaluate(objective, knowledgeBase));

        if (string.IsNullOrWhiteSpace(response))
        {
            return message;
        }

        return string.IsNullOrWhiteSpace(message) || !includeMessage
            ? response
            : $"{message}{Environment.NewLine}{response}";
    }

    public static ObjectiveCondition? FindPendingCondition(ObjectiveFormula objective) =>
        objective.Conditions.FirstOrDefault(condition =>
            condition.Role == ObjectiveConditionRole.HardConstraint &&
            condition.Value is null &&
            condition.ExpectedValueKind != ObjectiveConditionValueKind.None);

    private static bool IsExpectedValue(
        ObjectiveConditionValueKind expectedKind,
        ObjectiveConditionValue value) =>
        expectedKind switch
        {
            ObjectiveConditionValueKind.Money => value is ObjectiveMoneyValue,
            ObjectiveConditionValueKind.Text => value is ObjectiveTextValue,
            ObjectiveConditionValueKind.TimeWindow => value is ObjectiveTimeWindowValue,
            _ => false
        };

}
