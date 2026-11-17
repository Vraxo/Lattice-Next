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
    private static readonly Lazy<SemanticWorldModel> SharedSemanticWorldModel = new(
        SemanticWorldModelResourceLoader.Load,
        LazyThreadSafetyMode.ExecutionAndPublication);
    private static readonly Lazy<SemanticWorldModelReasoner> SharedSemanticWorldModelReasoner = new(
        () => new SemanticWorldModelReasoner(SharedSemanticWorldModel.Value),
        LazyThreadSafetyMode.ExecutionAndPublication);
    private readonly SemanticWorldModel semanticWorldModel;
    private readonly SemanticWorldModelReasoner semanticWorldModelReasoner;
    private readonly List<KnowledgePack> loadedPacks = [];
    private ObjectiveFormula? activeObjective;
    private string? activeObjectiveSource;
    private ObjectiveCondition? pendingObjectiveCondition;
    private readonly ObjectiveConversationWorkflow objectiveWorkflow;
    private readonly LatticeRuntimeQuestionProcessor questionProcessor;
    private bool isDisposed;

    public LatticeRuntimeSession()
    {
        objectiveWorkflow = new ObjectiveConversationWorkflow(
            knowledgeBase,
            objectiveEvaluator,
            () => activeObjective,
            value => activeObjective = value,
            () => activeObjectiveSource,
            value => activeObjectiveSource = value,
            () => pendingObjectiveCondition,
            value => pendingObjectiveCondition = value);
        semanticWorldModel = SharedSemanticWorldModel.Value;
        semanticWorldModelReasoner = SharedSemanticWorldModelReasoner.Value;
        questionProcessor = new LatticeRuntimeQuestionProcessor(
            knowledgeBase,
            reasoningEngine,
            semanticWorldModel,
            semanticWorldModelReasoner);
    }





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

    public ValueTask<LatticeCandidateEvidenceResult> AddReviewedCandidateMoneyEvidenceAsync(
        LatticeCandidateMoneyEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        lock (gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (isDisposed)
            {
                return ValueTask.FromResult(
                    LatticeCandidateEvidenceResult.Rejected("The session has been closed."));
            }

            if (activeObjective is null)
            {
                return ValueTask.FromResult(
                    LatticeCandidateEvidenceResult.Rejected("There is no active objective to assess this candidate evidence against."));
            }

            if (string.IsNullOrWhiteSpace(evidence.Candidate) ||
                string.IsNullOrWhiteSpace(evidence.CurrencySymbol) ||
                evidence.Amount < 0m ||
                string.IsNullOrWhiteSpace(evidence.SourceText) ||
                evidence.SourceText.Length > 4000 ||
                evidence.RetrievedAt == default ||
                evidence.RetrievedAt > DateTimeOffset.UtcNow.AddMinutes(5) ||
                (evidence.FreshUntil is { } freshUntil && freshUntil <= DateTimeOffset.UtcNow) ||
                !Uri.TryCreate(evidence.SourceUrl, UriKind.Absolute, out var sourceUri) ||
                sourceUri.Scheme is not ("http" or "https") ||
                string.IsNullOrWhiteSpace(sourceUri.Host) ||
                !string.IsNullOrEmpty(sourceUri.UserInfo) ||
                !sourceUri.IsDefaultPort)
            {
                return ValueTask.FromResult(
                    LatticeCandidateEvidenceResult.Rejected("The reviewed candidate evidence contains an invalid value or source."));
            }

            var candidateAttribute = knowledgeBase.Attributes.FirstOrDefault(attribute =>
                string.Equals(attribute.Subject.Value, evidence.Candidate, StringComparison.OrdinalIgnoreCase));

            if (candidateAttribute is null)
            {
                return ValueTask.FromResult(
                    LatticeCandidateEvidenceResult.Rejected("The reviewed evidence does not identify a known candidate."));
            }

            var candidate = candidateAttribute.Subject;
            var hasExistingCost = knowledgeBase.Attributes.Any(attribute =>
                attribute.Subject == candidate &&
                string.Equals(attribute.Name.Value, "total-cost", StringComparison.OrdinalIgnoreCase));
            var hasOutstandingRequest = objectiveEvaluator
                .Evaluate(activeObjective, knowledgeBase)
                .EvidenceRequests
                .Any(request =>
                    string.Equals(request.Candidate.Value, candidate.Value, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(request.AttributeName, "total-cost", StringComparison.OrdinalIgnoreCase));

            if (!hasExistingCost && !hasOutstandingRequest)
            {
                return ValueTask.FromResult(
                    LatticeCandidateEvidenceResult.Rejected("The active objective has no outstanding total-cost evidence request for that candidate."));
            }

            var matchingConditions = activeObjective.Conditions
                .Where(condition =>
                    condition.Role == ObjectiveConditionRole.HardConstraint &&
                    string.Equals(condition.TargetAttribute, "total-cost", StringComparison.OrdinalIgnoreCase) &&
                    condition.Value is ObjectiveMoneyValue)
                .ToArray();

            if (matchingConditions.Length != 1)
            {
                return ValueTask.FromResult(
                    LatticeCandidateEvidenceResult.Rejected("The active objective must contain exactly one monetary total-cost constraint."));
            }

            var objectiveMoney = (ObjectiveMoneyValue)matchingConditions[0].Value!;
            if (!string.Equals(objectiveMoney.CurrencySymbol, evidence.CurrencySymbol, StringComparison.Ordinal))
            {
                return ValueTask.FromResult(
                    LatticeCandidateEvidenceResult.Rejected("The evidence currency does not match the active objective's budget currency."));
            }

            var attribute = new KnowledgeAttribute(
                candidate,
                new SemanticIdentifier("total-cost"),
                new ObjectiveMoneyValue(evidence.Amount, evidence.CurrencySymbol),
                evidence.SourceText,
                KnowledgeSourceKind.ExternalObservation,
                SourceId: sourceUri.AbsoluteUri)
            {
                RetrievedAt = evidence.RetrievedAt,
                FreshUntil = evidence.FreshUntil
            };

            if (!knowledgeBase.TryAddAttribute(attribute, out var error))
            {
                return ValueTask.FromResult(
                    LatticeCandidateEvidenceResult.Rejected(error ?? "The reviewed candidate evidence could not be stored."));
            }

            return ValueTask.FromResult(
                LatticeCandidateEvidenceResult.Accepted(candidate.Value, "total-cost"));
        }
    }

    public ValueTask<LatticeCandidateEvidenceResult> AddReviewedCandidateAttributeEvidenceAsync(
        LatticeCandidateAttributeEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        lock (gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (isDisposed)
            {
                return ValueTask.FromResult(
                    LatticeCandidateEvidenceResult.Rejected("The session has been closed."));
            }

            if (activeObjective is null)
            {
                return ValueTask.FromResult(
                    LatticeCandidateEvidenceResult.Rejected("There is no active objective to assess this candidate evidence against."));
            }

            if (string.IsNullOrWhiteSpace(evidence.Candidate) ||
                evidence.Candidate.Length > 256 ||
                string.IsNullOrWhiteSpace(evidence.AttributeName) ||
                evidence.AttributeName.Length > 64 ||
                evidence.Value.ValueKind is System.Text.Json.JsonValueKind.Undefined or System.Text.Json.JsonValueKind.Null ||
                string.IsNullOrWhiteSpace(evidence.SourceText) ||
                evidence.SourceText.Length > 4000 ||
                evidence.RetrievedAt == default ||
                evidence.RetrievedAt > DateTimeOffset.UtcNow.AddMinutes(5) ||
                (evidence.FreshUntil is { } freshUntil && freshUntil <= DateTimeOffset.UtcNow) ||
                !Uri.TryCreate(evidence.SourceUrl, UriKind.Absolute, out var sourceUri) ||
                sourceUri.Scheme is not ("http" or "https") ||
                string.IsNullOrWhiteSpace(sourceUri.Host) ||
                !string.IsNullOrEmpty(sourceUri.UserInfo) ||
                !sourceUri.IsDefaultPort)
            {
                return ValueTask.FromResult(
                    LatticeCandidateEvidenceResult.Rejected("The reviewed candidate attribute evidence contains an invalid value or source."));
            }

            if (!KnowledgePackLoader.TryParseTypedAttributeValue(
                    evidence.Value,
                    out var typedValue,
                    out var typedValueError) ||
                typedValue is null)
            {
                return ValueTask.FromResult(
                    LatticeCandidateEvidenceResult.Rejected(
                        typedValueError ?? "The reviewed candidate attribute value is invalid."));
            }

            var candidateAttribute = knowledgeBase.Attributes.FirstOrDefault(attribute =>
                string.Equals(attribute.Subject.Value, evidence.Candidate, StringComparison.OrdinalIgnoreCase));

            if (candidateAttribute is null)
            {
                return ValueTask.FromResult(
                    LatticeCandidateEvidenceResult.Rejected("The reviewed evidence does not identify a known candidate."));
            }

            var candidate = candidateAttribute.Subject;
            var matchingConditions = activeObjective.Conditions
                .Where(condition =>
                    string.Equals(condition.TargetAttribute, evidence.AttributeName, StringComparison.OrdinalIgnoreCase) &&
                    condition.Value is not null)
                .ToArray();

            if (matchingConditions.Length == 0)
            {
                return ValueTask.FromResult(
                    LatticeCandidateEvidenceResult.Rejected("The active objective does not use that candidate attribute."));
            }

            var expectedTypes = matchingConditions
                .Select(condition => condition.Value!.GetType())
                .Distinct()
                .ToArray();

            if (expectedTypes.Length != 1 || expectedTypes[0] != typedValue!.GetType())
            {
                return ValueTask.FromResult(
                    LatticeCandidateEvidenceResult.Rejected("The reviewed value type does not match the active objective's candidate attribute."));
            }

            if (typedValue is ObjectiveMoneyValue money &&
                matchingConditions
                    .Select(condition => condition.Value)
                    .OfType<ObjectiveMoneyValue>()
                    .Any(expected => !string.Equals(expected.CurrencySymbol, money.CurrencySymbol, StringComparison.Ordinal)))
            {
                return ValueTask.FromResult(
                    LatticeCandidateEvidenceResult.Rejected("The evidence currency does not match the active objective's currency."));
            }

            var hasExistingAttribute = knowledgeBase.Attributes.Any(attribute =>
                attribute.Subject == candidate &&
                string.Equals(attribute.Name.Value, evidence.AttributeName, StringComparison.OrdinalIgnoreCase));
            var hasOutstandingRequest = objectiveEvaluator
                .Evaluate(activeObjective, knowledgeBase)
                .EvidenceRequests
                .Any(request =>
                    string.Equals(request.Candidate.Value, candidate.Value, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(request.AttributeName, evidence.AttributeName, StringComparison.OrdinalIgnoreCase));
            var hasPreferenceTarget = matchingConditions.Any(condition =>
                condition.Role == ObjectiveConditionRole.Preference);

            if (!hasExistingAttribute && !hasOutstandingRequest && !hasPreferenceTarget)
            {
                return ValueTask.FromResult(
                    LatticeCandidateEvidenceResult.Rejected("The active objective has no outstanding request or existing value for that candidate attribute."));
            }

            var attribute = new KnowledgeAttribute(
                candidate,
                new SemanticIdentifier(evidence.AttributeName),
                typedValue!,
                evidence.SourceText,
                KnowledgeSourceKind.ExternalObservation,
                SourceId: sourceUri.AbsoluteUri)
            {
                RetrievedAt = evidence.RetrievedAt,
                FreshUntil = evidence.FreshUntil,
                RequiresFreshness = true
            };

            if (!knowledgeBase.TryAddAttribute(attribute, out var error))
            {
                return ValueTask.FromResult(
                    LatticeCandidateEvidenceResult.Rejected(error ?? "The reviewed candidate attribute evidence could not be stored."));
            }

            return ValueTask.FromResult(
                LatticeCandidateEvidenceResult.Accepted(candidate.Value, evidence.AttributeName));
        }
    }

    public ValueTask<LatticeSessionStatus> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var evaluation = activeObjective is null
                ? null
                : objectiveEvaluator.Evaluate(activeObjective, knowledgeBase);

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
                ObjectiveEvaluation = evaluation is null ? null : LatticeRuntimeSessionObjectiveMapper.MapObjectiveEvaluation(evaluation),
                ObjectiveExplanation = evaluation is null ? null : ObjectiveEvaluationFormatter.Format(evaluation),
                SemanticConceptCount = semanticWorldModel.Concepts.Length,
                SemanticRelationCount = semanticWorldModel.Relations.Length,
                SemanticDataAssertionCount = semanticWorldModel.DataAssertions.Length
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

        for (var index = 0; index < parseResult.Utterances.Length; index++)
        {
            var parsedUtterance = parseResult.Utterances[index];
            var deferObjectiveEvaluation = parseResult.Utterances
                .Skip(index + 1)
                .Any(IsObjectiveUpdate);

            results.Add(ProcessUtterance(parsedUtterance, deferObjectiveEvaluation));
        }

        return LatticeTurnResult.Completed(results.ToImmutable());
    }

    private static bool IsObjectiveUpdate(SemanticUtterance utterance) =>
        utterance.Kind is UtteranceKind.ObjectiveCandidateInformation
            or UtteranceKind.ObjectiveInformation
            or UtteranceKind.ObjectiveConditionUpdate;

    private LatticeUtteranceResult ProcessUtterance(
        SemanticUtterance parsedUtterance,
        bool deferObjectiveEvaluation)
    {
        if (parsedUtterance.Kind == UtteranceKind.TaskRequest &&
            parsedUtterance.Meaning is TaskRequestFormula taskRequest)
        {
            return ProcessTaskRequest(parsedUtterance, taskRequest);
        }

        if (parsedUtterance.Kind == UtteranceKind.Question &&
            parsedUtterance.Meaning is EnumerationRemainderQuestionFormula enumerationRemainder)
        {
            return questionProcessor.ProcessEnumerationRemainderQuestion(parsedUtterance, enumerationRemainder);
        }

        if (parsedUtterance.Kind == UtteranceKind.Question &&
            parsedUtterance.Meaning is ChargedParticleAccelerationFormula chargedParticleAcceleration)
        {
            return questionProcessor.ProcessChargedParticleAccelerationQuestion(parsedUtterance, chargedParticleAcceleration);
        }

        if (parsedUtterance.Kind == UtteranceKind.Question &&
            parsedUtterance.Meaning is SemanticWorldModelQuestionFormula semanticQuestion)
        {
            return questionProcessor.ProcessSemanticWorldModelQuestion(parsedUtterance, semanticQuestion);
        }

        if (parsedUtterance.Kind == UtteranceKind.Question &&
            parsedUtterance.Meaning is SemanticRelationQueryFormula relationQuery)
        {
            return questionProcessor.ProcessRelationQueryQuestion(parsedUtterance, relationQuery);
        }

        if (parsedUtterance.Kind == UtteranceKind.Question &&
            parsedUtterance.Meaning is SemanticRelationSourceQueryFormula relationSourceQuery)
        {
            return questionProcessor.ProcessRelationSourceQueryQuestion(parsedUtterance, relationSourceQuery);
        }

        if (parsedUtterance.Kind == UtteranceKind.Question &&
            parsedUtterance.Meaning is MultiItemClassificationFormula classification)
        {
            return questionProcessor.ProcessClassificationQuestion(parsedUtterance, classification);
        }

        if (parsedUtterance.Kind == UtteranceKind.Question &&
            parsedUtterance.Meaning is ChoiceQuestionFormula choiceQuestion)
        {
            return questionProcessor.ProcessChoiceQuestion(parsedUtterance, choiceQuestion);
        }

        if (parsedUtterance.Kind == UtteranceKind.Question &&
            parsedUtterance.Meaning is DefinitionQuestionFormula definition)
        {
            return questionProcessor.ProcessDefinitionQuestion(parsedUtterance, definition);
        }

        if (parsedUtterance.Kind == UtteranceKind.ObjectiveCandidateInformation &&
            parsedUtterance.Meaning is ObjectiveCandidateInformationFormula candidateInformation)
        {
            return objectiveWorkflow.ProcessCandidateInformation(
                parsedUtterance,
                candidateInformation,
                deferObjectiveEvaluation);
        }

        if (parsedUtterance.Kind == UtteranceKind.ObjectiveInformation &&
            parsedUtterance.Meaning is ObjectiveInformationFormula information)
        {
            return objectiveWorkflow.ProcessInformation(
                parsedUtterance,
                information,
                deferObjectiveEvaluation);
        }

        if (parsedUtterance.Kind == UtteranceKind.ObjectiveConditionUpdate &&
            parsedUtterance.Meaning is ObjectiveConditionUpdateFormula conditionUpdate)
        {
            return objectiveWorkflow.ProcessConditionUpdates(
                parsedUtterance,
                conditionUpdate,
                deferObjectiveEvaluation);
        }

        if (parsedUtterance.Kind == UtteranceKind.Objective &&
            parsedUtterance.Meaning is ObjectiveFormula objective)
        {
            activeObjective = objective;
            activeObjectiveSource = parsedUtterance.SourceText;
            pendingObjectiveCondition = ObjectiveConversationWorkflow.FindPendingCondition(objective);

            if (pendingObjectiveCondition is not null)
            {
                return new LatticeUtteranceResult(
                    LatticeUtteranceResultKind.ClarificationRequired,
                    pendingObjectiveCondition.MissingValuePrompt ??
                        $"What value should I use for {pendingObjectiveCondition.Name}?",
                    SemanticFormatter.Format(parsedUtterance));
            }

            var message = objectiveWorkflow.AppendObjectiveEvaluation(
                $"I've retained this objective: {objective.Goal}.",
                objective,
                includeMessage: false);

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

        if (utterance.Meaning is PredicateApplication membershipQuery &&
            membershipQuery.Arguments.Length == 1 &&
            membershipQuery.Arguments[0] is IndividualTerm individual &&
            SemanticWorldModelResponder.TryAskMembership(
                individual.Identifier.Value,
                membershipQuery.Predicate.Value,
                semanticWorldModel,
                semanticWorldModelReasoner,
                out var membershipEvidence))
        {
            conversationContext.Observe(utterance);
            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.QuestionAnswered,
                Meaning: SemanticFormatter.Format(utterance),
                TruthValue: LatticeTruthValue.True,
                SupportingEvidence: membershipEvidence);
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
            SupportingEvidence: answer.Proofs.Select(LatticeRuntimeSessionObjectiveMapper.MapEvidence).ToImmutableArray(),
            OpposingEvidence: answer.ContradictingProofs.Select(LatticeRuntimeSessionObjectiveMapper.MapEvidence).ToImmutableArray());
    }

    private LatticeUtteranceResult ProcessTaskRequest(
        SemanticUtterance utterance,
        TaskRequestFormula request)
    {
        if (!TaskRequestResponder.TryRespond(
            request,
            semanticWorldModel,
            semanticWorldModelReasoner,
            out var message,
            out var evidence))
        {
            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.InformationUnavailable,
                "I don't have enough structured knowledge to complete this request.",
                SemanticFormatter.Format(utterance));
        }

        return new LatticeUtteranceResult(
            LatticeUtteranceResultKind.TaskCompleted,
            message,
            SemanticFormatter.Format(utterance),
            SupportingEvidence: evidence.IsDefault
                ? ImmutableArray<LatticeEvidence>.Empty
                : evidence,
            OpposingEvidence: ImmutableArray<LatticeEvidence>.Empty);
    }

























    private static string GetClarificationMessage(ReferenceResolutionStatus status) =>
        status switch
        {
            ReferenceResolutionStatus.MissingReferent =>
                "I don't know who 'it' refers to yet. Please name the individual.",
            ReferenceResolutionStatus.AmbiguousReferent =>
                "More than one individual is known. Which one do you mean by 'it'? Please name them.",
            _ => "I couldn't resolve that reference. Please name the individual."
        };
}
