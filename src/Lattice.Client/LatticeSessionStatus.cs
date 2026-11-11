namespace Lattice.Client;

/// <summary>
/// Read-only counts and pending conversational state for the current session.
/// </summary>
public sealed record LatticeSessionStatus(
    int FactCount,
    int RuleCount,
    int KnownIndividualCount,
    int LoadedKnowledgePackCount)
{
    public string? ActiveObjective { get; init; }

    public string? ActiveObjectiveSource { get; init; }

    public string? PendingInformationName { get; init; }

    public string? PendingInformationPrompt { get; init; }

    public LatticeObjectiveEvaluation? ObjectiveEvaluation { get; init; }

    public LatticeObjectiveEvaluation? ObjectiveEvaluation { get; init; }
}
