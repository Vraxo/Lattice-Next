using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

public sealed record UniversalRule(
    ImmutableArray<PredicateApplication> Premises,
    PredicateApplication Conclusion,
    string SourceText,
    KnowledgeSourceKind SourceKind = KnowledgeSourceKind.UserAssertion,
    ImmutableArray<KnowledgeSource> AdditionalSources = default,
    string? SourceId = null)
{
    public ImmutableArray<KnowledgeSource> Sources =>
        ImmutableArray.Create(new KnowledgeSource(SourceKind, SourceText, SourceId: SourceId))
            .AddRange(AdditionalSources.IsDefault
                ? ImmutableArray<KnowledgeSource>.Empty
                : AdditionalSources);
}
