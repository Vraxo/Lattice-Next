using System.Collections.Immutable;
using System.Globalization;
using Lattice.Client;
using Lattice.Core.Context;
using Lattice.Core.Intelligence;
using Lattice.Core.Language;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

namespace Lattice.Runtime;

internal sealed class LatticeRuntimeSession : ILatticeSession
{
    private readonly object gate = new();
    private readonly LceParser parser = new();
    private readonly ConversationContext conversationContext = new();
    private readonly KnowledgeBase knowledgeBase = new();
    private readonly KnowledgePackLoader knowledgePackLoader = new();
    private readonly ReasoningEngine reasoningEngine = new();
    private readonly ObjectiveEvaluator objectiveEvaluator = new();
    private readonly List<KnowledgePack> loadedPacks = [];
    private ObjectiveFormula? activeObjective;
    private string? activeObjectiveSource;
    private ObjectiveCondition? pendingObjectiveCondition;
    private bool isDisposed;

    public Guid SessionId { get; } = Guid.NewGuid();

    public ValueTask<LatticeTurnResult> SubmitAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (isDisposed)
            {
                return ValueTask.FromResult(
                    LatticeTurnResult.Rejected("The session has been closed."));
            }

            return ValueTask.FromResult(Process(input));
        }
    }

    public ValueTask<LatticeKnowledgePackResult> LoadKnowledgePackAsync(
        string json,
        CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (isDisposed)
            {
                return ValueTask.FromResult(
                    LatticeKnowledgePackResult.Rejected("The session has been closed."));
            }

            if (!knowledgePackLoader.TryLoadAndAdd(
                json,
                knowledgeBase,
                out var pack,
                out var error))
            {
                return ValueTask.FromResult(
                    LatticeKnowledgePackResult.Rejected(
                        error ?? "The knowledge pack could not be loaded."));
            }

            loadedPacks.Add(pack!);
            return ValueTask.FromResult(
                LatticeKnowledgePackResult.Loaded(pack!.Name, pack.ClaimCount));
        }
    }

    public ValueTask<LatticeSessionStatus> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.FromResult(new LatticeSessionStatus(
                knowledgeBase.Facts.Count,
                knowledgeBase.Rules.Count,
                conversationContext.KnownIndividualCount,
                loadedPacks.Count)
            {
                ActiveObjective = activeObjective?.Goal,
                ActiveObjectiveSource = activeObjectiveSource,
                PendingInformationName = pendingObjectiveCondition?.Name,
                PendingInformationPrompt = pendingObjectiveCondition?.MissingValuePrompt,
                ObjectiveEvaluation = activeObjective is null
                    ? null
                    : MapObjectiveEvaluation(objectiveEvaluator.Evaluate(activeObjective, knowledgeBase))
            });
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (gate)
        {
            isDisposed = true;
        }

        return ValueTask.CompletedTask;
    }

    private LatticeTurnResult Process(string input)
    {
        var parseResult = parser.ParseText(input);

        if (!parseResult.IsSuccess)
        {
            return LatticeTurnResult.Rejected(
                parseResult.Error ?? "Input could not be parsed.");
        }

        var results = ImmutableArray.CreateBuilder<LatticeUtteranceResult>(
            parseResult.Utterances.Length);

        foreach (var parsedUtterance in parseResult.Utterances)
        {
            results.Add(ProcessUtterance(parsedUtterance));
        }

        return LatticeTurnResult.Completed(results.ToImmutable());
    }

    private LatticeUtteranceResult ProcessUtterance(SemanticUtterance parsedUtterance)
    {
        if (parsedUtterance.Kind == UtteranceKind.ObjectiveInformation &&
            parsedUtterance.Meaning is ObjectiveInformationFormula information)
        {
            return ProcessObjectiveInformation(parsedUtterance, information);
        }

        if (parsedUtterance.Kind == UtteranceKind.ObjectiveConditionUpdate &&
            parsedUtterance.Meaning is ObjectiveConditionUpdateFormula conditionUpdate)
        {
            return ProcessObjectiveConditionUpdates(parsedUtterance, conditionUpdate);
        }

        if (parsedUtterance.Kind == UtteranceKind.Objective &&
            parsedUtterance.Meaning is ObjectiveFormula objective)
        {
            activeObjective = objective;
            activeObjectiveSource = parsedUtterance.SourceText;
            pendingObjectiveCondition = FindPendingObjectiveCondition(objective);

            if (pendingObjectiveCondition is not null)
            {
                return new LatticeUtteranceResult(
                    LatticeUtteranceResultKind.ClarificationRequired,
                    pendingObjectiveCondition.MissingValuePrompt ??
                        $"What value should I use for {pendingObjectiveCondition.Name}?",
                    SemanticFormatter.Format(parsedUtterance));
            }

            var message = $"I've retained this objective: {objective.Goal}.";
            message = AppendObjectiveEvaluation(message, objective);

            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.ObjectiveReceived,
                message,
                SemanticFormatter.Format(parsedUtterance));
        }

        var resolution = conversationContext.Resolve(parsedUtterance);

        if (!resolution.IsSuccess)
        {
            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.ClarificationRequired,
                GetClarificationMessage(resolution.Status),
                ReferenceStatus: resolution.Status switch
                {
                    ReferenceResolutionStatus.MissingReferent =>
                        LatticeReferenceStatus.MissingReferent,
                    ReferenceResolutionStatus.AmbiguousReferent =>
                        LatticeReferenceStatus.AmbiguousReferent,
                    _ => null
                });
        }

        var utterance = resolution.Utterance!;

        if (utterance.Kind == UtteranceKind.Assertion)
        {
            if (!knowledgeBase.TryAdd(utterance, out var kind, out var error))
            {
                return new LatticeUtteranceResult(
                    LatticeUtteranceResultKind.StatementRejected,
                    error ?? "The statement could not be stored.",
                    SemanticFormatter.Format(utterance));
            }

            conversationContext.Observe(utterance);

            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.StatementStored,
                Meaning: SemanticFormatter.Format(utterance),
                EntryKind: kind switch
                {
                    KnowledgeEntryKind.Fact => LatticeKnowledgeEntryKind.Fact,
                    KnowledgeEntryKind.Rule => LatticeKnowledgeEntryKind.Rule,
                    _ => null
                });
        }

        var answer = utterance.Meaning switch
        {
            PredicateApplication predicateQuery =>
                reasoningEngine.Ask(knowledgeBase, predicateQuery),
            Negation negatedQuery =>
                reasoningEngine.Ask(knowledgeBase, negatedQuery),
            ExistentialQuantification existentialQuery =>
                reasoningEngine.Ask(knowledgeBase, existentialQuery),
            UniversalQuantification universalQuery =>
                reasoningEngine.Ask(knowledgeBase, universalQuery),
            _ => null
        };

        if (answer is null)
        {
            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.UnsupportedQuestion,
                "Unsupported question form.",
                SemanticFormatter.Format(utterance));
        }

        conversationContext.Observe(utterance);

        return new LatticeUtteranceResult(
            LatticeUtteranceResultKind.QuestionAnswered,
            Meaning: SemanticFormatter.Format(utterance),
            TruthValue: answer.TruthValue switch
            {
                TruthValue.True => LatticeTruthValue.True,
                TruthValue.False => LatticeTruthValue.False,
                TruthValue.Both => LatticeTruthValue.Both,
                _ => LatticeTruthValue.Unknown
            },
            SupportingEvidence: answer.Proofs.Select(MapEvidence).ToImmutableArray(),
            OpposingEvidence: answer.ContradictingProofs.Select(MapEvidence).ToImmutableArray());
    }

    private LatticeUtteranceResult ProcessObjectiveInformation(
        SemanticUtterance parsedUtterance,
        ObjectiveInformationFormula information)
    {
        var objective = activeObjective;
        var pendingCondition = pendingObjectiveCondition;

        if (objective is null || pendingCondition is null)
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
        activeObjective = objective;
        var originalSource = activeObjectiveSource ?? parsedUtterance.SourceText;
        activeObjectiveSource = $"{originalSource}{Environment.NewLine}{parsedUtterance.SourceText}";
        pendingObjectiveCondition = FindPendingObjectiveCondition(objective);

        var message = $"Recorded {parsedUtterance.SourceText} for {answeredCondition.Name}.";

        if (pendingObjectiveCondition is not null)
        {
            message = $"{message} {pendingObjectiveCondition.MissingValuePrompt}";
        }
        else
        {
            message = AppendObjectiveEvaluation(message, objective);
        }

        return new LatticeUtteranceResult(
            LatticeUtteranceResultKind.ObjectiveInformationReceived,
            message,
            SemanticFormatter.Format(new SemanticUtterance(
                UtteranceKind.Objective,
                objective,
                activeObjectiveSource)));
    }

    private LatticeUtteranceResult ProcessObjectiveConditionUpdates(
        SemanticUtterance parsedUtterance,
        ObjectiveConditionUpdateFormula update)
    {
        var objective = activeObjective;

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
        activeObjective = objective;
        var originalSource = activeObjectiveSource ?? parsedUtterance.SourceText;
        activeObjectiveSource = $"{originalSource}{Environment.NewLine}{parsedUtterance.SourceText}";
        pendingObjectiveCondition = FindPendingObjectiveCondition(objective);

        var names = string.Join(", ", update.Conditions.Select(condition => condition.Name));
        var message = AppendObjectiveEvaluation(
            $"Updated objective conditions: {names}.",
            objective);

        return new LatticeUtteranceResult(
            LatticeUtteranceResultKind.ObjectiveInformationReceived,
            message,
            SemanticFormatter.Format(new SemanticUtterance(
                UtteranceKind.Objective,
                objective,
                activeObjectiveSource)));
    }

    private string AppendObjectiveEvaluation(string message, ObjectiveFormula objective)
    {
        var summary = ObjectiveEvaluationFormatter.Format(
            objectiveEvaluator.Evaluate(objective, knowledgeBase));

        return string.IsNullOrWhiteSpace(summary)
            ? message
            : $"{message}{Environment.NewLine}{summary}";
    }

    private static ObjectiveCondition? FindPendingObjectiveCondition(ObjectiveFormula objective) =>
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

    private static string GetClarificationMessage(ReferenceResolutionStatus status) =>
        status switch
        {
            ReferenceResolutionStatus.MissingReferent =>
                "I don't know who 'it' refers to yet. Please name the individual.",
            ReferenceResolutionStatus.AmbiguousReferent =>
                "More than one individual is known. Which one do you mean by 'it'? Please name them.",
            _ => "I couldn't resolve that reference. Please name the individual."
        };

    private static LatticeObjectiveEvaluation MapObjectiveEvaluation(ObjectiveEvaluation evaluation) =>
        new(evaluation.Candidates.Select(candidate => new LatticeCandidateAssessment(
            candidate.Candidate.Value,
            candidate.HasHardConstraintViolation,
            candidate.HasUnresolvedHardConstraint,
            candidate.Conditions.Select(condition => new LatticeObjectiveConditionAssessment(
                condition.ConditionName,
                condition.Role.ToString(),
                condition.Status.ToString(),
                condition.Evidence
                    .SelectMany(attribute => attribute.Sources)
                    .Distinct()
                    .Select(source => new LatticeObjectiveEvidenceSource(
                        source.Kind.ToString(),
                        source.SourceText,
                        source.SourceId))
                    .ToImmutableArray())
            {
                TemporalWitness = condition.TemporalWitness.Select(stage => new LatticeObjectiveTemporalStage(
                    stage.Index,
                    stage.DurationAttribute,
                    stage.Start.ToString("HH:mm", CultureInfo.InvariantCulture),
                    stage.End.ToString("HH:mm", CultureInfo.InvariantCulture),
                    stage.DurationMinutes))
                    .ToImmutableArray(),
                Preferences = condition.Preferences.Select(preference => new LatticeObjectivePreferenceAssessment(
                    preference.Preference,
                    preference.Status.ToString(),
                    preference.Evidence
                        .SelectMany(attribute => attribute.Sources)
                        .Distinct()
                        .Select(source => new LatticeObjectiveEvidenceSource(
                            source.Kind.ToString(),
                            source.SourceText,
                            source.SourceId))
                        .ToImmutableArray()))
                    .ToImmutableArray()
            })
                .ToImmutableArray())
        {
            Feasibility = candidate.Feasibility.ToString(),
            Highlights = candidate.Highlights.Select(highlight =>
                new LatticeObjectiveCandidateHighlight(
                    highlight.Name.Value,
                    MapObjectiveEvidenceSources(new[] { highlight })))
                .ToImmutableArray(),
            MissingHardConstraintAttributes = candidate.MissingHardConstraintAttributes
        })
            .ToImmutableArray())
        {
            PreferenceTradeOffs = evaluation.PreferenceTradeOffs.Select(tradeOff =>
                new LatticeObjectivePreferenceTradeOff(
                    tradeOff.Preference,
                    tradeOff.CandidateWithPositiveEvidence.Value,
                    tradeOff.CandidateWithContraryEvidence.Value,
                    MapObjectiveEvidenceSources(tradeOff.PositiveEvidence),
                    MapObjectiveEvidenceSources(tradeOff.ContraryEvidence)))
                .ToImmutableArray(),
            Recommendation = new LatticeObjectiveRecommendation(
                evaluation.Recommendation.Status.ToString(),
                evaluation.Recommendation.ShortlistedCandidates.Select(candidate => candidate.Value).ToImmutableArray(),
                evaluation.Recommendation.DominatedCandidates.Select(candidate => candidate.Value).ToImmutableArray(),
                evaluation.Recommendation.HasIncompletePreferenceEvidence)
        };

    private static ImmutableArray<LatticeObjectiveEvidenceSource> MapObjectiveEvidenceSources(
        IEnumerable<KnowledgeAttribute> attributes) =>
        attributes
            .SelectMany(attribute => attribute.Sources)
            .Distinct()
            .Select(source => new LatticeObjectiveEvidenceSource(
                source.Kind.ToString(),
                source.SourceText,
                source.SourceId))
            .ToImmutableArray();

    private static LatticeEvidence MapEvidence(ProofNode proof)
    {
        var premises = ImmutableArray.CreateBuilder<LatticeEvidence>();

        if (proof.Premise is not null)
        {
            premises.Add(MapEvidence(proof.Premise));
        }

        if (!proof.AdditionalPremises.IsDefaultOrEmpty)
        {
            foreach (var premise in proof.AdditionalPremises)
            {
                premises.Add(MapEvidence(premise));
            }
        }

        return new LatticeEvidence(
            proof.Kind switch
            {
                ProofNodeKind.Asserted => LatticeEvidenceKind.Asserted,
                _ => LatticeEvidenceKind.Derived
            },
            proof.Polarity switch
            {
                EvidencePolarity.Positive => LatticeEvidencePolarity.Positive,
                _ => LatticeEvidencePolarity.Negative
            },
            proof.Sources.Select(source => new LatticeEvidenceSource(
                source.Kind switch
                {
                    KnowledgeSourceKind.UserAssertion => LatticeKnowledgeSourceKind.UserAssertion,
                    KnowledgeSourceKind.ImportedClaim => LatticeKnowledgeSourceKind.ImportedClaim,
                    _ => LatticeKnowledgeSourceKind.ExternalObservation
                },
                source.SourceText,
                source.SourceId,
                source.ResolvedReferent?.Value)).ToImmutableArray(),
            premises.ToImmutable());
    }
}
