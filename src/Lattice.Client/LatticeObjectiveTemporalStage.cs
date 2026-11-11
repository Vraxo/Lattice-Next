namespace Lattice.Client;

/// <summary>
/// One scheduled stage in a candidate's feasible temporal sequence.
/// </summary>
public sealed record LatticeObjectiveTemporalStage(
    int Index,
    string DurationAttribute,
    string Start,
    string End,
    int DurationMinutes);
