using System.Collections.Immutable;

namespace Lattice.Core.Generation;

public sealed record SequenceSemanticContext(
    ImmutableArray<string> RequiredFacts,
    ImmutableArray<string> InitialFacts)
{
    public static SequenceSemanticContext Empty { get; } =
        new(ImmutableArray<string>.Empty, ImmutableArray<string>.Empty);
}
