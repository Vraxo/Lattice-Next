using System.Collections.Immutable;

namespace Lattice.Core.Reasoning;

public sealed record ReasoningResult(ProofNode? Proof)
{
    public ImmutableArray<ProofNode> AdditionalProofs { get; init; } =
        ImmutableArray<ProofNode>.Empty;

    public ImmutableArray<ProofNode> ContradictingProofs { get; init; } =
        ImmutableArray<ProofNode>.Empty;

    public ImmutableArray<ProofNode> Proofs =>
        Proof is null
            ? ImmutableArray<ProofNode>.Empty
            : ImmutableArray.Create(Proof).AddRange(
                AdditionalProofs.IsDefault
                    ? ImmutableArray<ProofNode>.Empty
                    : AdditionalProofs);

    public TruthValue TruthValue =>
        (Proof is not null, !ContradictingProofs.IsDefaultOrEmpty) switch
        {
            (true, false) => global::Lattice.Core.Reasoning.TruthValue.True,
            (false, true) => global::Lattice.Core.Reasoning.TruthValue.False,
            (true, true) => global::Lattice.Core.Reasoning.TruthValue.Both,
            _ => global::Lattice.Core.Reasoning.TruthValue.Unknown
        };

    public bool IsSupported => Proof is not null;
}
