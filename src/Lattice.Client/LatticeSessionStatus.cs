namespace Lattice.Client;

/// <summary>
/// Read-only counts describing the current session.
/// </summary>
public sealed record LatticeSessionStatus(
    int FactCount,
    int RuleCount,
    int KnownIndividualCount,
    int LoadedKnowledgePackCount);
