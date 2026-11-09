using System.Diagnostics;
using System.ComponentModel;

namespace Lattice.Translators.LmStudio;

public sealed class LmStudioProcessRunner : ILmStudioProcessRunner
{
    private readonly List<Process> backgroundProcesses = new();

    public bool TryStartDetached(
        string executable,
        IReadOnlyList<string> arguments,
        out string? error)
    {
        error = null;
        var startInfo = CreateStartInfo(executable, arguments);
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        try
        {
            var process = Process.Start(startInfo);

            if (process is null)
            {
                error = $"Could not start '{executable}'.";
                return false;
            }

            process.OutputDataReceived += static (_, _) => { };
            process.ErrorDataReceived += static (_, _) => { };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            backgroundProcesses.Add(process);
            return true;
        }
        catch (Exception exception) when (
            exception is Win32Exception or FileNotFoundException or InvalidOperationException)
        {
            error = $"Could not start '{executable}': {exception.Message}";
            return false;
        }
    }

    public async Task<LmStudioProcessResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var startInfo = CreateStartInfo(executable, arguments);
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        using var process = Process.Start(startInfo);

        if (process is null)
        {
            throw new InvalidOperationException($"Could not start '{executable}'.");
        }

        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            throw new TimeoutException(
                $"'{executable} {string.Join(' ', arguments)}' did not finish within {timeout.TotalSeconds:0} seconds.");
        }

        var output = await standardOutput.ConfigureAwait(false);
        var error = await standardError.ConfigureAwait(false);

        return new LmStudioProcessResult(process.ExitCode, output, error);
    }

    private static ProcessStartInfo CreateStartInfo(
        string executable,
        IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }
}
