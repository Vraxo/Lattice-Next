namespace Lattice.Core.Intelligence;

public sealed record ToolIntentResolution(
    ToolIntentResolutionStatus Status,
    ToolIntent? Intent)
{
    public bool IsResolved => Status == ToolIntentResolutionStatus.Resolved;
}
