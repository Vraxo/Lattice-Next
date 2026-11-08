using System.Collections.Immutable;

namespace Lattice.Core.Semantics;

public sealed record DescriptiveIndividualTerm(
    SemanticIdentifier Head,
    ImmutableArray<SemanticIdentifier> Modifiers) : SemanticTerm
{
    public SemanticIdentifier Identifier =>
        new(string.Join("-", Modifiers.Select(modifier => modifier.Value).Append(Head.Value)));
}
