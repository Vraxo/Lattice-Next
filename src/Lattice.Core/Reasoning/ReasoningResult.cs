using System.Collections.Immutable;

namespace Lattice.Core.Reasoning;

public sealed record ReasoningResult(ProofNode? Proof)
{
    public ImmutableArray<ProofNode> AdditionalProofs { get; init; } = ImmutableArray<ProofNode>.Empty;
    public ProofNode? NegativeProof { get; init; }
    public ImmutableArray<ProofNode> AdditionalNegativeProofs { get; init; } = ImmutableArray<ProofNode>.Empty;

    public ImmutableArray<ProofNode> Proofs =>
        Proof is null ? ImmutableArray<ProofNode>.Empty :
        ImmutableArray.Create(Proof).AddRange(AdditionalProofs.IsDefault ? ImmutableArray<ProofNode>.Empty : AdditionalProofs);

    public ImmutableArray<ProofNode> NegativeProofs =>
        NegativeProof is null ? ImmutableArray<ProofNode>.Empty :
        ImmutableArray.Create(NegativeProof).AddRange(AdditionalNegativeProofs.IsDefault ? ImmutableArray<ProofNode>.Empty : AdditionalNegativeProofs);

    public TruthStatus TruthStatus =>
        (Proofs.IsEmpty, NegativeProofs.IsEmpty) switch
        {
            (false, false) => Lattice.Core.Reasoning.TruthStatus.Both,
            (false, true) => Lattice.Core.Reasoning.TruthStatus.True,
            (true, false) => Lattice.Core.Reasoning.TruthStatus.False,
            _ => TruthStatus.Neither
        };

    public bool IsSupported => !Proofs.IsEmpty;
}
