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
    ProofNodeKind Kind = ProofNodeKind.Asserted)
{
    public ImmutableArray<KnowledgeSource> Sources =>
        ImmutableArray.Create(new KnowledgeSource(SourceKind, SourceText, ResolvedReferent))
            .AddRange(AdditionalSources.IsDefault
                ? ImmutableArray<KnowledgeSource>.Empty
                : AdditionalSources);
}