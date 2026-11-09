using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

public sealed record KnowledgeFact(
    PredicateApplication Predicate,
    string SourceText,
    SemanticIdentifier? ResolvedReferent = null,
    KnowledgeSourceKind SourceKind = KnowledgeSourceKind.UserAssertion,
    ImmutableArray<KnowledgeSource> AdditionalSources = default,
    string? SourceId = null)
{
    public ImmutableArray<KnowledgeSource> Sources =>
        ImmutableArray.Create(new KnowledgeSource(SourceKind, SourceText, ResolvedReferent, SourceId))
            .AddRange(AdditionalSources.IsDefault
                ? ImmutableArray<KnowledgeSource>.Empty
                : AdditionalSources);
}
