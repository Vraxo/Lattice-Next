namespace Lattice.Core.Reasoning;

/// <summary>
/// One stage in a concrete temporal sequence that satisfies a CanFit condition.
/// </summary>
public sealed record ObjectiveTemporalStageResult(
    int Index,
    string DurationAttribute,
    TimeOnly Start,
    TimeOnly End,
    int DurationMinutes);
