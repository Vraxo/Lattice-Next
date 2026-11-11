namespace Lattice.Core.Semantics;

public sealed record ObjectiveTemporalStage(
    string DurationAttribute,
    string? StartTimesAttribute = null,
    string? AvailabilityWindowAttribute = null);
