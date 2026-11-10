using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

public sealed record ProofNode(
    PredicateApplication Predicate,
    string SourceText,
    ProofNode? Premise,
    SemanticIdentifier? ResolvedReferent = null,
    ImmutableArray<ProofNode> AdditionalPremises = default,
    KnowledgeSourceKind SourceKind = KnowledgeSourceKind.UserAssertion,
    ImmutableArray<KnowledgeSource> AdditionalSources = default,
    ProofNodeKind Kind = ProofNodeKind.Asserted,
    string? SourceId = null)
{
    public EvidencePolarity Polarity { get; init; } = EvidencePolarity.Positive;

    public ImmutableArray<KnowledgeSource> Sources =>
        ImmutableArray.Create(new KnowledgeSource(SourceKind, SourceText, ResolvedReferent, SourceId))
            .AddRange(AdditionalSources.IsDefault
                ? ImmutableArray<KnowledgeSource>.Empty
                : AdditionalSources);
}
