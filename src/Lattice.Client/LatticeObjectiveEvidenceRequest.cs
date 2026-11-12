namespace Lattice.Client;

/// <summary>A candidate-specific fact needed to assess a hard constraint.</summary>
public sealed record LatticeObjectiveEvidenceRequest(
    string Candidate,
    string ConditionName,
    string AttributeName,
    string Prompt,
    string ExpectedValueKind);
