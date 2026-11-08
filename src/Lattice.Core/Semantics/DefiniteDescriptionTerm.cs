using System.Collections.Immutable;

namespace Lattice.Core.Semantics;

public sealed record DefiniteDescriptionTerm(
    SemanticIdentifier Head,
    ImmutableArray<SemanticIdentifier> Modifiers) : SemanticTerm;
