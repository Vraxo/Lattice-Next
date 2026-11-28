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

        // Execute the actual response, not the expected source above.
        var lines = utterance.Message.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        await Execute(string.Join('\n', lines[1..^1]), string.Empty);
    }

    [Theory]
    [InlineData("surface", 160, 120)]
    [InlineData("canvas", 240, 180)]
    public async Task Request_dimensions_and_result_names_reach_the_generated_window(string name, int width, int height)
    {
        await GenerateAndExecute(
            $"Write a Python program using Pygame. Initialize Pygame. Invoke api pygame-window with arguments ([{width}, {height}]) saving result as {name}. " +
            $"Invoke api pygame-surface-size with arguments ({name}) saving result as dimensions then print dimensions. Quit Pygame.",
            $"({width}, {height})\n");
    }

    [Fact]
    public async Task Consumes_request_bound_timing_results_in_arithmetic()
    {
        await GenerateAndExecute(
            "Write a Python program using Pygame. Initialize Pygame. Invoke api pygame-clock saving result as timer. " +
            "Invoke api pygame-tick with arguments (timer, 0) saving result as elapsed. Set seconds to elapsed * 0.001 then when seconds is at least 0 then print 'timed'. Quit Pygame.",
            "timed\n");
    }

    [Fact]
    public async Task Composes_api_result_bindings_with_iteration_and_member_conditions()
    {
        await GenerateAndExecute(
            "Write a Python program using Pygame. Initialize Pygame. Invoke api pygame-window with arguments ([80, 60]) saving result as screen. " +
            "Invoke api pygame-quit-event saving result as quit_kind. Invoke api pygame-event with arguments (quit_kind) saving result as notification. " +
            "Invoke api pygame-post-event with arguments (notification). Invoke api pygame-events saving result as notifications then for each event in notifications then when event.type is equal to quit_kind then print 'quit observed'. " +
            "End repetition. Quit Pygame.",
            "quit observed\n");
    }

    private static async Task GenerateAndExecute(string request, string expected)
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();
        var result = await session.SubmitAsync(request);
        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, utterance.Kind);
        var fence = new string('`', 3);
        var lines = utterance.Message!.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var start = Array.IndexOf(lines, fence + "python");
        var end = Array.IndexOf(lines, fence, start + 1);
        Assert.True(start >= 0 && end > start);
        await Execute(string.Join('\n', lines[(start + 1)..end]), expected);
    }

    private static async Task Execute(string code, string expected)
    {
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
        info.ArgumentList.Add(code);
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
        Assert.Equal(expected, (await output).Replace("\r\n", "\n", StringComparison.Ordinal));
    }
}
