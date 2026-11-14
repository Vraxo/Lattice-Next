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
    private readonly SemanticWorldModel semanticWorldModel;
    private readonly SemanticWorldModelReasoner semanticWorldModelReasoner;
    private readonly List<KnowledgePack> loadedPacks = [];
    private ObjectiveFormula? activeObjective;
    private string? activeObjectiveSource;
    private ObjectiveCondition? pendingObjectiveCondition;
    private bool isDisposed;

    public LatticeRuntimeSession()
    {
        var semanticJson = LoadEmbeddedSemanticWorldModel();
        if (!new SemanticWorldModelLoader().TryLoad(
            semanticJson,
            out var loadedSemanticWorldModel,
            out var error))
        {
            throw new InvalidOperationException(
                error ?? "The embedded semantic world model could not be loaded.");
        }

        semanticWorldModel = loadedSemanticWorldModel!;
        semanticWorldModelReasoner = new SemanticWorldModelReasoner(semanticWorldModel);
    }

    private static string LoadEmbeddedSemanticWorldModel()
    {
        const string resourcePrefix = "Lattice.Runtime.SemanticWorldModel.";
        var assembly = typeof(LatticeRuntimeSession).Assembly;
        var resources = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(resourcePrefix, StringComparison.Ordinal) &&
                name.EndsWith(".json", StringComparison.Ordinal))
            .OrderBy(name => name.EndsWith(".core.json", StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(name => name, StringComparer.Ordinal)
            .ToArray();

        if (resources.Length == 0)
        {
            throw new InvalidOperationException("No embedded semantic world model modules were found.");
        }

        System.Text.Json.Nodes.JsonObject? combined = null;
        var collectionNames = new[]
        {
            "concepts", "lexemes", "relations", "definitions",
            "relationAssertions", "dataProperties", "dataAssertions"
        };

        foreach (var resource in resources)
        {
            using var stream = assembly.GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException($"The semantic world model module '{resource}' is missing.");
            using var reader = new StreamReader(stream);
            var module = System.Text.Json.Nodes.JsonNode.Parse(reader.ReadToEnd()) as System.Text.Json.Nodes.JsonObject
                ?? throw new InvalidOperationException($"The semantic world model module '{resource}' is invalid.");

            if (combined is null)
            {
                combined = (System.Text.Json.Nodes.JsonObject)module.DeepClone();
                continue;
            }

            foreach (var collectionName in collectionNames)
            {
                if (module[collectionName] is not System.Text.Json.Nodes.JsonArray source ||
                    combined[collectionName] is not System.Text.Json.Nodes.JsonArray target)
                {
                    throw new InvalidOperationException(
                        $"The semantic world model module '{resource}' is missing '{collectionName}'.");
                }

                foreach (var item in source)
                {
                    target.Add(item?.DeepClone());
                }
            }
        }

        return combined?.ToJsonString()
            ?? throw new InvalidOperationException("The embedded semantic world model is empty.");
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
                ObjectiveEvaluation = evaluation is null ? null : MapObjectiveEvaluation(evaluation),
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
            pendingObjectiveCondition = FindPendingObjectiveCondition(objective);

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
            SupportingEvidence: answer.Proofs.Select(MapEvidence).ToImmutableArray(),
            OpposingEvidence: answer.ContradictingProofs.Select(MapEvidence).ToImmutableArray());
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
            LatticeUtteranceResultKind.QuestionAnswered,
            message,
            SemanticFormatter.Format(utterance),
            SupportingEvidence: evidence);
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

    private LatticeUtteranceResult ProcessSemanticWorldModelQuestion(
        SemanticUtterance utterance,
        SemanticWorldModelQuestionFormula question)
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

    private LatticeUtteranceResult ProcessObjectiveCandidateInformation(
        SemanticUtterance parsedUtterance,
        ObjectiveCandidateInformationFormula information,
        bool deferObjectiveEvaluation)
    {
        var objective = activeObjective;

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

        var originalSource = activeObjectiveSource ?? objective.Goal;
        activeObjectiveSource = $"{originalSource}{Environment.NewLine}{parsedUtterance.SourceText}";

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

    private LatticeUtteranceResult ProcessObjectiveInformation(
        SemanticUtterance parsedUtterance,
        ObjectiveInformationFormula information,
        bool deferObjectiveEvaluation)
    {
        var objective = activeObjective;

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
            : pendingObjectiveCondition;

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
        activeObjective = objective;
        var originalSource = activeObjectiveSource ?? parsedUtterance.SourceText;
        activeObjectiveSource = $"{originalSource}{Environment.NewLine}{parsedUtterance.SourceText}";
        pendingObjectiveCondition = FindPendingObjectiveCondition(objective);

        var message = $"Recorded {parsedUtterance.SourceText} for {answeredCondition.Name}.";

        if (pendingObjectiveCondition is not null)
        {
            message = $"{message} {pendingObjectiveCondition.MissingValuePrompt}";
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
                activeObjectiveSource)));
    }

    private LatticeUtteranceResult ProcessObjectiveConditionUpdates(
        SemanticUtterance parsedUtterance,
        ObjectiveConditionUpdateFormula update,
        bool deferObjectiveEvaluation)
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
        activeObjective = objective;
        var originalSource = activeObjectiveSource ?? parsedUtterance.SourceText;
        activeObjectiveSource = $"{originalSource}{Environment.NewLine}{parsedUtterance.SourceText}";
        pendingObjectiveCondition = FindPendingObjectiveCondition(objective);

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
                activeObjectiveSource)));
    }

    private string AppendObjectiveEvaluation(
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
                        source.SourceId,
                        source.RetrievedAt,
                        source.FreshUntil,
                        source.RequiresFreshness))
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
                            source.SourceId,
                            source.RetrievedAt,
                            source.FreshUntil,
                            source.RequiresFreshness))
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
                .ToImmutableArray()
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
                evaluation.Recommendation.HasIncompletePreferenceEvidence),
            EvidenceRequests = evaluation.EvidenceRequests.Select(request =>
                new LatticeObjectiveEvidenceRequest(
                    request.Candidate.Value,
                    request.ConditionName,
                    request.AttributeName,
                    request.Prompt))
                .ToImmutableArray()
        };

    private static ImmutableArray<LatticeObjectiveEvidenceSource> MapObjectiveEvidenceSources(
        IEnumerable<KnowledgeAttribute> attributes) =>
        attributes
            .SelectMany(attribute => attribute.Sources)
            .Distinct()
            .Select(source => new LatticeObjectiveEvidenceSource(
                source.Kind.ToString(),
                source.SourceText,
                source.SourceId,
                source.RetrievedAt,
                source.FreshUntil,
                source.RequiresFreshness))
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
