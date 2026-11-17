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
    private readonly ObjectiveConversationWorkflow objectiveWorkflow;

    private ObjectiveFormula? activeObjective
    {
        get => objectiveWorkflow.ActiveObjective;
        set => objectiveWorkflow.ActiveObjective = value;
    }

    private string? activeObjectiveSource
    {
        get => objectiveWorkflow.ActiveObjectiveSource;
        set => objectiveWorkflow.ActiveObjectiveSource = value;
    }

    private ObjectiveCondition? pendingObjectiveCondition
    {
        get => objectiveWorkflow.PendingCondition;
        set => objectiveWorkflow.PendingCondition = value;
    }
    private bool isDisposed;

    public LatticeRuntimeSession()
    {
        objectiveWorkflow = new ObjectiveConversationWorkflow(knowledgeBase, objectiveEvaluator);
        semanticWorldModel = SharedSemanticWorldModel.Value;
        semanticWorldModelReasoner = SharedSemanticWorldModelReasoner.Value;
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
            return ProcessEnumerationRemainderQuestion(parsedUtterance, enumerationRemainder);
        }

        if (parsedUtterance.Kind == UtteranceKind.Question &&
            parsedUtterance.Meaning is ChargedParticleAccelerationFormula chargedParticleAcceleration)
        {
            return ProcessChargedParticleAccelerationQuestion(parsedUtterance, chargedParticleAcceleration);
        }

        if (parsedUtterance.Kind == UtteranceKind.Question &&
            parsedUtterance.Meaning is SemanticWorldModelQuestionFormula semanticQuestion)
        {
            return ProcessSemanticWorldModelQuestion(parsedUtterance, semanticQuestion);
        }

        if (parsedUtterance.Kind == UtteranceKind.Question &&
            parsedUtterance.Meaning is SemanticRelationQueryFormula relationQuery)
        {
            return ProcessRelationQueryQuestion(parsedUtterance, relationQuery);
        }

        if (parsedUtterance.Kind == UtteranceKind.Question &&
            parsedUtterance.Meaning is SemanticRelationSourceQueryFormula relationSourceQuery)
        {
            return ProcessRelationSourceQueryQuestion(parsedUtterance, relationSourceQuery);
        }

        if (parsedUtterance.Kind == UtteranceKind.Question &&
            parsedUtterance.Meaning is MultiItemClassificationFormula classification)
        {
            return ProcessClassificationQuestion(parsedUtterance, classification);
        }

        if (parsedUtterance.Kind == UtteranceKind.Question &&
            parsedUtterance.Meaning is ChoiceQuestionFormula choiceQuestion)
        {
            return ProcessChoiceQuestion(parsedUtterance, choiceQuestion);
        }

        if (parsedUtterance.Kind == UtteranceKind.Question &&
            parsedUtterance.Meaning is DefinitionQuestionFormula definition)
        {
            return ProcessDefinitionQuestion(parsedUtterance, definition);
        }

        if (parsedUtterance.Kind == UtteranceKind.ObjectiveCandidateInformation &&
            parsedUtterance.Meaning is ObjectiveCandidateInformationFormula candidateInformation)
        {
            return ProcessObjectiveCandidateInformation(
                parsedUtterance,
                candidateInformation,
                deferObjectiveEvaluation);
        }

        if (parsedUtterance.Kind == UtteranceKind.ObjectiveInformation &&
            parsedUtterance.Meaning is ObjectiveInformationFormula information)
        {
            return ProcessObjectiveInformation(
                parsedUtterance,
                information,
                deferObjectiveEvaluation);
        }

        if (parsedUtterance.Kind == UtteranceKind.ObjectiveConditionUpdate &&
            parsedUtterance.Meaning is ObjectiveConditionUpdateFormula conditionUpdate)
        {
            return ProcessObjectiveConditionUpdates(
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

            var message = AppendObjectiveEvaluation(
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

    private LatticeUtteranceResult ProcessClassificationQuestion(
        SemanticUtterance utterance,
        MultiItemClassificationFormula request)
    {
        if (SemanticWorldModelResponder.TryClassify(
            request,
            semanticWorldModel,
            semanticWorldModelReasoner,
            out var semanticMessage,
            out var semanticEvidence))
        {
            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.QuestionAnswered,
                semanticMessage,
                SemanticFormatter.Format(utterance),
                SupportingEvidence: semanticEvidence);
        }

        if (!MultiItemClassificationResponder.TryRespond(
            request,
            knowledgeBase,
            reasoningEngine,
            out var message,
            out var evidence))
        {
            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.InformationUnavailable,
                "I don't have enough evidence to classify every item into exactly one requested category.",
                SemanticFormatter.Format(utterance),
                SupportingEvidence: ImmutableArray<LatticeEvidence>.Empty);
        }

        return new LatticeUtteranceResult(
            LatticeUtteranceResultKind.QuestionAnswered,
            message,
            SemanticFormatter.Format(utterance),
            SupportingEvidence: evidence);
    }

    private LatticeUtteranceResult ProcessChoiceQuestion(
        SemanticUtterance utterance,
        ChoiceQuestionFormula question)
    {
        if (SemanticWorldModelResponder.TryChoice(
            question,
            semanticWorldModel,
            semanticWorldModelReasoner,
            out var semanticMessage,
            out var semanticEvidence))
        {
            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.QuestionAnswered,
                semanticMessage,
                SemanticFormatter.Format(utterance),
                SupportingEvidence: semanticEvidence);
        }

        if (!ChoiceQuestionResponder.TryRespond(
            question,
            knowledgeBase,
            reasoningEngine,
            out var message,
            out var evidence))
        {
            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.InformationUnavailable,
                "I don't have enough evidence to distinguish the available choices.",
                SemanticFormatter.Format(utterance),
                SupportingEvidence: ImmutableArray<LatticeEvidence>.Empty);
        }

        return new LatticeUtteranceResult(
            LatticeUtteranceResultKind.QuestionAnswered,
            message,
            SemanticFormatter.Format(utterance),
            SupportingEvidence: evidence);
    }

    private LatticeUtteranceResult ProcessDefinitionQuestion(
        SemanticUtterance utterance,
        DefinitionQuestionFormula definition)
    {
        var requestedConcept = string.Join(
            " ",
            (definition.Modifiers.IsDefaultOrEmpty
                ? ImmutableArray<SemanticIdentifier>.Empty
                : definition.Modifiers)
                .Append(definition.Concept)
                .Select(identifier => identifier.Value));

        var meaning = SemanticFormatter.Format(utterance);

        if (SemanticWorldModelResponder.TryDefinition(
            requestedConcept,
            semanticWorldModel,
            semanticWorldModelReasoner,
            out var semanticMessage,
            out var semanticEvidence))
        {
            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.QuestionAnswered,
                semanticMessage,
                meaning,
                SupportingEvidence: semanticEvidence);
        }

        if (!ConceptKnowledgeFormatter.TryCreate(knowledgeBase, reasoningEngine, requestedConcept, out var explanation))
        {
            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.InformationUnavailable,
                $"I don't have structured knowledge about {requestedConcept} in the available knowledge.",
                meaning,
                SupportingEvidence: ImmutableArray<LatticeEvidence>.Empty);
        }

        return new LatticeUtteranceResult(
            LatticeUtteranceResultKind.QuestionAnswered,
            explanation!.Message,
            meaning,
            SupportingEvidence: explanation.Evidence);
    }

    private LatticeUtteranceResult ProcessEnumerationRemainderQuestion(
        SemanticUtterance utterance,
        EnumerationRemainderQuestionFormula question)
    {
        var ordinal = question.MissingPosition switch
        {
            1 => "first",
            2 => "second",
            3 => "third",
            4 => "fourth",
            5 => "fifth",
            6 => "sixth",
            7 => "seventh",
            8 => "eighth",
            9 => "ninth",
            10 => "tenth",
            _ => $"{question.MissingPosition}{GetOrdinalSuffix(question.MissingPosition)}"
        };
        var group = SingularizeEnumerationGroup(question.Group);
        var member = char.ToUpperInvariant(question.Member[0]) + question.Member[1..];
        var requiredType = group switch
        {
            "daughter" => "female-person",
            "son" => "male-person",
            "child" => "person",
            _ => null
        };

        if (requiredType is not null)
        {
            var anchor = semanticWorldModel.FindConcept(question.Member, SemanticConceptKind.Individual);
            if (anchor is null ||
                question.ExpectedCount < 2 ||
                question.MissingPosition != question.ExpectedCount ||
                question.NamedMembers.IsDefault ||
                question.NamedMembers.Length != question.ExpectedCount - 1 ||
                question.NamedMembers.Distinct(StringComparer.Ordinal).Count() != question.NamedMembers.Length ||
                question.NamedMembers.Contains(question.Member, StringComparer.Ordinal) ||
                !semanticWorldModelReasoner.IsInstanceOf(anchor.Id, requiredType))
            {
                return new LatticeUtteranceResult(
                    LatticeUtteranceResultKind.InformationUnavailable,
                    "I cannot identify the missing family member from the stated relationships and available type knowledge.",
                    SemanticFormatter.Format(utterance),
                    SupportingEvidence: ImmutableArray<LatticeEvidence>.Empty);
            }

            var membershipPremise = new LatticeEvidence(
                LatticeEvidenceKind.Asserted,
                LatticeEvidencePolarity.Positive,
                semanticWorldModelReasoner.GetMembershipEvidence(anchor.Id, requiredType)
                    .Select(source => new LatticeEvidenceSource(
                        LatticeKnowledgeSourceKind.FoundationalKnowledge,
                        $"{anchor.Label} is modeled as a {group}.",
                        source.SourceId,
                        null))
                    .ToImmutableArray(),
                ImmutableArray<LatticeEvidence>.Empty);
            var userPremise = new LatticeEvidence(
                LatticeEvidenceKind.Asserted,
                LatticeEvidencePolarity.Positive,
                ImmutableArray.Create(new LatticeEvidenceSource(
                    LatticeKnowledgeSourceKind.UserAssertion,
                    utterance.SourceText,
                    "family-set-description",
                    null)),
                ImmutableArray<LatticeEvidence>.Empty);
            var derivedEvidence = new LatticeEvidence(
                LatticeEvidenceKind.Derived,
                LatticeEvidencePolarity.Positive,
                ImmutableArray<LatticeEvidenceSource>.Empty,
                ImmutableArray.Create(userPremise, membershipPremise));

            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.QuestionAnswered,
                $"The {ordinal} {group} is {member}.",
                SemanticFormatter.Format(utterance),
                SupportingEvidence: ImmutableArray.Create(derivedEvidence));
        }
        var source = new LatticeEvidenceSource(
            LatticeKnowledgeSourceKind.UserAssertion,
            utterance.SourceText,
            SourceId: null,
            ResolvedReferent: null);
        var evidence = new LatticeEvidence(
            LatticeEvidenceKind.Derived,
            LatticeEvidencePolarity.Positive,
            ImmutableArray.Create(source),
            ImmutableArray<LatticeEvidence>.Empty);

        return new LatticeUtteranceResult(
            LatticeUtteranceResultKind.QuestionAnswered,
            $"The {ordinal} {group} is {member}.",
            SemanticFormatter.Format(utterance),
            SupportingEvidence: ImmutableArray.Create(evidence));
    }

    private static string SingularizeEnumerationGroup(string group) =>
        group switch
        {
            "children" => "child",
            "people" => "person",
            "men" => "man",
            "women" => "woman",
            "mice" => "mouse",
            "geese" => "goose",
            "teeth" => "tooth",
            "feet" => "foot",
            _ when group.EndsWith("ies", StringComparison.Ordinal) => $"{group[..^3]}y",
            _ when group.EndsWith("sses", StringComparison.Ordinal) ||
                   group.EndsWith("shes", StringComparison.Ordinal) ||
                   group.EndsWith("ches", StringComparison.Ordinal) ||
                   group.EndsWith("xes", StringComparison.Ordinal) ||
                   group.EndsWith("zes", StringComparison.Ordinal) => group[..^2],
            _ when group.EndsWith("s", StringComparison.Ordinal) &&
                   !group.EndsWith("ss", StringComparison.Ordinal) => group[..^1],
            _ => group
        };

    private static string GetOrdinalSuffix(int value)
    {
        var lastTwoDigits = value % 100;
        if (lastTwoDigits is 11 or 12 or 13)
        {
            return "th";
        }

        return (value % 10) switch
        {
            1 => "st",
            2 => "nd",
            3 => "rd",
            _ => "th"
        };
    }

    private LatticeUtteranceResult ProcessChargedParticleAccelerationQuestion(
        SemanticUtterance utterance,
        ChargedParticleAccelerationFormula question)
    {
        var meaning = SemanticFormatter.Format(utterance);
        if (!SemanticWorldModelResponder.TryRespond(
            question,
            semanticWorldModel,
            semanticWorldModelReasoner,
            out var message,
            out var evidence))
        {
            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.InformationUnavailable,
                "I don't have enough structured physical data to calculate that final speed.",
                meaning,
                SupportingEvidence: ImmutableArray<LatticeEvidence>.Empty);
        }

        return new LatticeUtteranceResult(
            LatticeUtteranceResultKind.QuestionAnswered,
            message,
            meaning,
            SupportingEvidence: evidence);
    }

    private LatticeUtteranceResult ProcessSemanticWorldModelQuestion(
        SemanticUtterance utterance,
        SemanticWorldModelQuestionFormula question)
    {
        var meaning = SemanticFormatter.Format(utterance);
        if (question.Kind == SemanticWorldModelQuestionKind.Clarification)
        {
            var clarificationMessage = $"Which {question.Subject} are you referring to? Please list them, and I'll check each one against {question.OtherSubject}.";
            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.ClarificationRequired,
                clarificationMessage,
                meaning);
        }

        if (!SemanticWorldModelResponder.TryRespond(
            question,
            semanticWorldModel,
            semanticWorldModelReasoner,
            out var message,
            out var evidence))
        {
            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.InformationUnavailable,
                "I don't have enough structured semantic knowledge to answer that question.",
                meaning,
                SupportingEvidence: ImmutableArray<LatticeEvidence>.Empty);
        }

        return new LatticeUtteranceResult(
            LatticeUtteranceResultKind.QuestionAnswered,
            message,
            meaning,
            SupportingEvidence: evidence);
    }

    private LatticeUtteranceResult ProcessRelationQueryQuestion(
        SemanticUtterance utterance,
        SemanticRelationQueryFormula question)
    {
        var meaning = SemanticFormatter.Format(utterance);
        if (!SemanticWorldModelResponder.TryRespond(
            question,
            semanticWorldModel,
            semanticWorldModelReasoner,
            out var message,
            out var evidence))
        {
            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.InformationUnavailable,
                "I don't have enough structured semantic knowledge to answer that question.",
                meaning,
                SupportingEvidence: ImmutableArray<LatticeEvidence>.Empty);
        }

        return new LatticeUtteranceResult(
            LatticeUtteranceResultKind.QuestionAnswered,
            message,
            meaning,
            SupportingEvidence: evidence);
    }

    private LatticeUtteranceResult ProcessRelationSourceQueryQuestion(
        SemanticUtterance utterance,
        SemanticRelationSourceQueryFormula question)
    {
        var meaning = SemanticFormatter.Format(utterance);
        if (!SemanticWorldModelResponder.TryRespond(
            question,
            semanticWorldModel,
            semanticWorldModelReasoner,
            out var message,
            out var evidence))
        {
            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.InformationUnavailable,
                "I don't have enough structured semantic knowledge to answer that question.",
                meaning,
                SupportingEvidence: ImmutableArray<LatticeEvidence>.Empty);
        }

        return new LatticeUtteranceResult(
            LatticeUtteranceResultKind.QuestionAnswered,
            message,
            meaning,
            SupportingEvidence: evidence);
    }









ldn't resolve that reference. Please name the individual."
        };


}
