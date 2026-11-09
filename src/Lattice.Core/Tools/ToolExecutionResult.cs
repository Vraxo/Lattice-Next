namespace Lattice.Core.Tools;

public sealed record ToolExecutionResult(
    bool IsSuccess,
    string? Output,
    string? Error)
{
    public static ToolExecutionResult Success(string output) =>
        new(true, output, null);

    public static ToolExecutionResult Failure(string error) =>
        new(false, null, error);
}
