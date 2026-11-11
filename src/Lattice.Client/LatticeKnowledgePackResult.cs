namespace Lattice.Client;

/// <summary>
/// Describes the outcome of loading a knowledge pack into a session.
/// </summary>
public sealed record LatticeKnowledgePackResult(
    bool IsSuccess,
    string? Name = null,
    int ClaimCount = 0,
    string? Error = null)
{
    public static LatticeKnowledgePackResult Loaded(string name, int claimCount) =>
        new(true, name, claimCount);

    public static LatticeKnowledgePackResult Rejected(string error) =>
        new(false, Error: error);
}
