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
    public EvidencePolarity ConclusionPolarity { get; init; } = EvidencePolarity.Positive;

    public ImmutableArray<PredicateApplication> NegativePremises { get; init; } = [];

    public ImmutableArray<PredicateApplication> RelationalPremises { get; init; } = [];

    public ImmutableArray<PredicateApplication> NegativeRelationalPremises { get; init; } = [];

    public ExistentialRuleConclusion? ExistentialConclusion { get; init; }

    public ImmutableArray<KnowledgeSource> Sources =>
        ImmutableArray.Create(new KnowledgeSource(SourceKind, SourceText, SourceId: SourceId))
            .AddRange(AdditionalSources.IsDefault
                ? ImmutableArray<KnowledgeSource>.Empty
                : AdditionalSources);
}
