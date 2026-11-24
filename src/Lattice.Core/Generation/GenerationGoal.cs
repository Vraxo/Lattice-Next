using System.Collections.Immutable;

namespace Lattice.Core.Generation;

public sealed record GenerationGoal(
    string Effect,
    string ArgumentKind,
    string ArgumentValue,
    ImmutableArray<string> AvailableCapabilities)
{
    public ImmutableDictionary<string, string> Bindings { get; init; } =
        ImmutableDictionary<string, string>.Empty.WithComparers(StringComparer.Ordinal);

    public ImmutableDictionary<string, SemanticCollection> CollectionBindings { get; init; } =
        ImmutableDictionary<string, SemanticCollection>.Empty.WithComparers(StringComparer.Ordinal);
}
