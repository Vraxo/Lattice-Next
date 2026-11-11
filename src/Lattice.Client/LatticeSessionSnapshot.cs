namespace Lattice.Client;

/// <summary>
/// Read-only counts describing the current session.
/// </summary>
public sealed record LatticeSessionSnapshot(
    int FactCount,
    int RuleCount,
    int KnownIndividualCount,
    int KnowledgePackCount);

/// <summary>
/// Describes the outcome of loading a knowledge pack into a session.
/// </summary>
public sealed record LatticeKnowledgePackResult(
    bool IsSuccess,
    string? Name = null,
    string? Id = null,
    int ClaimCount = 0,
    string? Error = null)
{
    public static LatticeKnowledgePackResult Loaded(
        string name,
        string id,
        int claimCount) =>
        new(true, name, id, claimCount);

    public static LatticeKnowledgePackResult Rejected(string error) =>
        new(false, Error: error);
}
