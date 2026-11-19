using System.Collections.Immutable;

namespace Lattice.Core.Generation;

public sealed record SequenceTokenRule(
    string Id,
    ImmutableArray<string> ContextSuffix,
    ImmutableArray<string> RequiredFacts,
    ImmutableArray<string> RequiresCompletedFacts,
    string NextToken,
    ImmutableArray<string> SatisfiesFacts,
    double Weight = 0);

public sealed record SequenceKnowledgeBase(
    ImmutableArray<SequenceTokenRule> Rules)
{
    public const string StartToken = "<|lattice.start|>";
}
