using Lattice.Core.Semantics;

namespace Lattice.Core.Context;

public sealed record ReferenceResolutionResult(
    ReferenceResolutionStatus Status,
    SemanticUtterance? Utterance)
{
    public bool IsSuccess =>
        Status is ReferenceResolutionStatus.NotRequired or ReferenceResolutionStatus.Resolved;
}
