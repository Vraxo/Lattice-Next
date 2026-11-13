using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

public sealed record KnowledgeAttribute(
    SemanticIdentifier Subject,
    SemanticIdentifier Name,
    ObjectiveConditionValue Value,
    string SourceText,
    KnowledgeSourceKind SourceKind = KnowledgeSourceKind.UserAssertion,
    ImmutableArray<KnowledgeSource> AdditionalSources = default,
    string? SourceId = null)
{
    public bool IncludeInSummary { get; init; }

    public bool IsObjectiveCandidate { get; init; } = true;

    public DateTimeOffset? RetrievedAt { get; init; }

    public DateTimeOffset? FreshUntil { get; init; }

    public bool RequiresFreshness { get; init; }

    public ImmutableArray<KnowledgeSource> Sources =>
        ImmutableArray.Create(new KnowledgeSource(SourceKind, SourceText, SourceId: SourceId)
            {
                RetrievedAt = RetrievedAt,
                FreshUntil = FreshUntil,
                RequiresFreshness = RequiresFreshness || SourceKind == KnowledgeSourceKind.ExternalObservation
            })
            .AddRange(AdditionalSources.IsDefault
                ? ImmutableArray<KnowledgeSource>.Empty
                : AdditionalSources);
}
