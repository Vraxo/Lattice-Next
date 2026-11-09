using Lattice.Translators.LmStudio;

namespace Lattice.Translators.LmStudio.Tests;

internal sealed class FakeLmStudioProcessRunner : ILmStudioProcessRunner
{
    public List<(string Executable, string[] Arguments)> DetachedStarts { get; } = new();

    public List<(string Executable, string[] Arguments)> Runs { get; } = new();

    public Func<string, string[], LmStudioProcessResult>? RunResult { get; set; }

    public Action? OnStart { get; set; }

    public bool StartSucceeds { get; set; } = true;

    public string? StartError { get; set; }

    public bool TryStartDetached(
        string executable,
        IReadOnlyList<string> arguments,
        out string? error)
    {
        DetachedStarts.Add((executable, arguments.ToArray()));

        if (StartSucceeds)
        {
            OnStart?.Invoke();
        }

        error = StartError;
        return StartSucceeds;
    }

    public Task<LmStudioProcessResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var copy = arguments.ToArray();
        Runs.Add((executable, copy));
        var result = RunResult?.Invoke(executable, copy)
            ?? new LmStudioProcessResult(0, string.Empty, string.Empty);
        return Task.FromResult(result);
    }
}
