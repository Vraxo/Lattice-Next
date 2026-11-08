namespace Lattice.Core.Reasoning;

public sealed record ReasoningResult(ProofNode? Proof)
{
    public bool IsSupported => Proof is not null;
}
