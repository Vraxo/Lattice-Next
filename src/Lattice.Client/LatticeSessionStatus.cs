namespace Lattice.Client;

/// <summary>
/// Read-only counts describing the current session.
/// </summary>
public sealed record LatticeSessionStatus(
    int FactCount,
    int RuleCount,
    int KnownIndividualCount,
    int LoadedKnowledgePackCount)
{
    public string? ActiveObjective { get; init; }

    public string? ActiveObjectiveSource { get; init; }
}
