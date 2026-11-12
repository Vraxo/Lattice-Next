namespace Lattice.Client;

/// <summary>
/// Represents one isolated conversational session with Lattice.
/// </summary>
public interface ILatticeSession : IAsyncDisposable
{
    Guid SessionId { get; }

    ValueTask<LatticeTurnResult> SubmitAsync(
        string input,
        CancellationToken cancellationToken = default);

    ValueTask<LatticeKnowledgePackResult> LoadKnowledgePackAsync(
        string json,
        CancellationToken cancellationToken = default);

    ValueTask<LatticeCandidateEvidenceResult> AddReviewedCandidateMoneyEvidenceAsync(
        LatticeCandidateMoneyEvidence evidence,
        CancellationToken cancellationToken = default);

    ValueTask<LatticeSessionStatus> GetStatusAsync(
        CancellationToken cancellationToken = default);
}
