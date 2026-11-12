using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

public sealed record KnowledgeSource(
    KnowledgeSourceKind Kind,
    string SourceText,
    SemanticIdentifier? ResolvedReferent = null,
    string? SourceId = null)
{
    public DateTimeOffset? RetrievedAt { get; init; }

    public DateTimeOffset? FreshUntil { get; init; }
}
