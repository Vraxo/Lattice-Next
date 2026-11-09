using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Intelligence;

public sealed record SemanticTranslationResult(
    SemanticTranslationStatus Status,
    ImmutableArray<SemanticUtterance> Candidates,
    string? Message)
{
    public bool HasCandidates => !Candidates.IsDefaultOrEmpty;
}
