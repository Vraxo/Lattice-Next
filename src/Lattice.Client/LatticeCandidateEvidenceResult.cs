namespace Lattice.Client;

/// <summary>Describes whether reviewed candidate evidence was accepted.</summary>
public sealed record LatticeCandidateEvidenceResult(
    bool IsSuccess,
    string? Candidate = null,
    string? AttributeName = null,
    string? Error = null)
{
    public static LatticeCandidateEvidenceResult Accepted(string candidate, string attributeName) =>
        new(true, candidate, attributeName);

    public static LatticeCandidateEvidenceResult Rejected(string error) =>
        new(false, Error: error);
}
