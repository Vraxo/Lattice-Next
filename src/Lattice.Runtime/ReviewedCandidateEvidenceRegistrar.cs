using System.Collections.Immutable;
using Lattice.Client;
using Lattice.Core.Intelligence;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

namespace Lattice.Runtime;

internal sealed class ReviewedCandidateEvidenceRegistrar
{
    private readonly KnowledgeBase knowledgeBase;
    private readonly ObjectiveEvaluator objectiveEvaluator;
    private readonly Func<ObjectiveFormula?> getActiveObjective;

    public ReviewedCandidateEvidenceRegistrar(
        KnowledgeBase knowledgeBase,
        ObjectiveEvaluator objectiveEvaluator,
        Func<ObjectiveFormula?> getActiveObjective)
    {
        this.knowledgeBase = knowledgeBase;
        this.objectiveEvaluator = objectiveEvaluator;
        this.getActiveObjective = getActiveObjective;
    }

    public ValueTask<LatticeCandidateEvidenceResult> AddMoney(
        LatticeCandidateMoneyEvidence evidence)
    {
        var activeObjective = getActiveObjective();

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

    public ValueTask<LatticeCandidateEvidenceResult> AddAttribute(
        LatticeCandidateAttributeEvidence evidence)
    {
        var activeObjective = getActiveObjective();

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
