using System.Collections.Immutable;

namespace Lattice.Core.Reasoning;

public sealed record ReasoningResult(ProofNode? Proof)
{
    public ImmutableArray<ProofNode> AdditionalProofs { get; init; } =
        ImmutableArray<ProofNode>.Empty;

    public ImmutableArray<ProofNode> Proofs =>
        Proof is null
            ? ImmutableArray<ProofNode>.Empty
            : ImmutableArray.Create(Proof).AddRange(
                AdditionalProofs.IsDefault
                    ? ImmutableArray<ProofNode>.Empty
                    : AdditionalProofs);

    public bool IsSupported => Proof is not null;
}
