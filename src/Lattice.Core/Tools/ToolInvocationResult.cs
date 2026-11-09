namespace Lattice.Core.Tools;

public sealed record ToolInvocationResult(
    bool IsSuccess,
    ToolObservation? Observation,
    string? Error)
{
    public static ToolInvocationResult Success(ToolObservation observation) =>
        new(true, observation, null);

    public static ToolInvocationResult Failure(string error) =>
        new(false, null, error);
}
