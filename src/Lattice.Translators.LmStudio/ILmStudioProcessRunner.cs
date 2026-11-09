namespace Lattice.Translators.LmStudio;

public sealed record LmStudioProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);

public interface ILmStudioProcessRunner
{
    bool TryStartDetached(
        string executable,
        IReadOnlyList<string> arguments,
        out string? error);

    Task<LmStudioProcessResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
