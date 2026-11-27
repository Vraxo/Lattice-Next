using System.Diagnostics;
using Lattice.Client;
using Lattice.Runtime;
using Xunit;

namespace Lattice.Runtime.Tests;

public sealed class ApiRequestGenerationTests
{
    [Theory]
    [InlineData("Write a Python program using Pygame. Initialize Pygame. Quit Pygame.")]
    [InlineData("Create a Python program that import pygame then invoke api pygame-init then invoke api pygame-quit.")]
    public async Task Generates_and_executes_declared_setup_and_shutdown_from_a_normal_request(string request)
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();
        var result = await session.SubmitAsync(request);
        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, utterance.Kind);
        var fence = new string('`', 3);
        var expectedCode = "import pygame\npygame.init()\npygame.quit()";
        Assert.Equal(fence + "python\n" + expectedCode + "\n" + fence,
            utterance.Message!.Replace("\r\n", "\n", StringComparison.Ordinal));

        var info = new ProcessStartInfo(OperatingSystem.IsWindows() ? "python" : "python3")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        info.Environment["SDL_VIDEODRIVER"] = "dummy";
        info.Environment["SDL_AUDIODRIVER"] = "dummy";
        info.Environment["PYGAME_HIDE_SUPPORT_PROMPT"] = "1";
        info.ArgumentList.Add("-c");
        // Execute the actual response, not the expected source above.
        var lines = utterance.Message.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        info.ArgumentList.Add(string.Join('\n', lines[1..^1]));
        using var process = Process.Start(info);
        Assert.NotNull(process);
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }
        Assert.True(process.ExitCode == 0, await errors);
        Assert.Equal(string.Empty, await output);
    }
}
