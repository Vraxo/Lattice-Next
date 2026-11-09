using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

public sealed record KnowledgeFact(
    PredicateApplication Predicate,
    string SourceText,
    SemanticIdentifier? ResolvedReferent = null,
    KnowledgeSourceKind SourceKind = KnowledgeSourceKind.UserAssertion,
    ImmutableArray<KnowledgeSource> AdditionalSources = default)
{
    public ImmutableArray<KnowledgeSource> Sources =>
        ImmutableArray.Create(new KnowledgeSource(SourceKind, SourceText, ResolvedReferent))
            .AddRange(AdditionalSources.IsDefault
                ? ImmutableArray<KnowledgeSource>.Empty
                : AdditionalSources);
}