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

    [Theory]
    [InlineData(640, 480)]
    [InlineData(240, 180)]
    public async Task Executes_naturally_worded_window_setup_with_changed_dimensions(int width, int height)
    {
        var code = await GenerateAndExecute(
            $"Write a Python program using Pygame. Initialize Pygame, open a window that is {width} wide and {height} tall. " +
            "Invoke api pygame-surface-size with arguments (screen) saving result as dimensions then print dimensions. Quit Pygame.",
            $"({width}, {height})\n");
        Assert.Contains($"screen = pygame.display.set_mode([{width}, {height}])", code, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Window_arguments_can_be_computed_from_request_bound_state()
    {
        var code = await GenerateAndExecute(
            "Write a Python program using Pygame. Initialize Pygame. Set width to 80 and set height to 60. " +
            "Open a window that is width + 10 wide and height + 20 tall saving result as canvas. " +
            "Invoke api pygame-surface-size with arguments (canvas) saving result as dimensions then print dimensions. Quit Pygame.",
            "(90, 80)\n");
        Assert.Contains("canvas = pygame.display.set_mode([width + 10, height + 20])", code, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("screen", "black", "0")]
    [InlineData("canvas", "red", "255")]
    [InlineData("surface", "'white'", "765")]
    public async Task Executes_clear_and_present_with_request_derived_receivers_and_colors(string name, string color, string expected)
    {
        var code = await GenerateAndExecute(
            $"Write a Python program using Pygame. Initialize Pygame, open a window that is 32 wide and 24 tall saving result as {name}. " +
            $"Clear the {name} to {color}. Present the frame. " +
            $"Invoke api pygame-surface-pixel with arguments ({name}, [0, 0]) saving result as pixel then print pixel.r + pixel.g + pixel.b. Quit Pygame.",
            expected + "\n");
        Assert.Contains($"({name}).fill(", code, StringComparison.Ordinal);
        Assert.Contains("pygame.display.flip()", code, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Executes_clear_and_presentation_in_a_request_derived_frame_loop()
    {
        await GenerateAndExecute(
            "Write a Python program using Pygame. Initialize Pygame, open a window that is 32 wide and 24 tall. " +
            "Set count to 0 then while count is less than 2 then clear the screen to red. Present the frame. Set count to count + 1. End repetition. " +
            "Invoke api pygame-surface-pixel with arguments (screen, [0, 0]) saving result as pixel then print pixel.r. Quit Pygame.",
            "255\n");
    }

    [Theory]
    [InlineData("Limit", "clock", 60)]
    [InlineData("Cap", "timer", 30)]
    public async Task Executes_natural_timing_requests_inside_a_bounded_frame_loop(string verb, string name, int rate)
    {
        var code = await GenerateAndExecute(
            $"Write a Python program using Pygame. Initialize Pygame. Create a clock saving result as {name}. " +
            "Set count to 0 and set elapsed to 0 then while count is less than 2 then " +
            $"{verb} frames to {rate} frames per second using {name} saving result as elapsed. " +
            "Set count to count + 1. End repetition. Print count. When elapsed is at least 0 then print 'timed'. Quit Pygame.",
            "2\ntimed\n");
        Assert.Contains($"({name}).tick({rate})", code, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Executes_default_clock_and_computed_frame_rate()
    {
        await GenerateAndExecute(
            "Write a Python program using Pygame. Initialize Pygame. Create a clock. Set rate to 25. " +
            "Limit frames to rate * 2 frames per second using clock saving result as elapsed. Set seconds to elapsed / 1000. When seconds is at least 0 then print 'timed'. Quit Pygame.",
            "timed\n");
    }

    [Theory]
    [InlineData("marker", "red", 3, 4, 8, 9, 255)]
    [InlineData("panel", "white", 5, 6, 10, 12, 765)]
    public async Task Executes_rectangle_descriptions_and_checks_inside_and_outside_pixels(
        string label, string color, int x, int y, int width, int height, int expected)
    {
        await GenerateAndExecute(
            "Write a Python program using Pygame. Initialize Pygame. Open a window that is 64 wide and 48 tall. Clear the screen to black. " +
            $"Set xpos to {x} and set ypos to {y} and set rectWidth to {width} and set rectHeight to {height}. " +
            $"Draw the {label} as a {color} rectangle at x xpos and y ypos with width rectWidth and height rectHeight. Present the frame. " +
            "Invoke api pygame-surface-pixel with arguments (screen, [xpos, ypos]) saving result as inside then print inside.r + inside.g + inside.b. " +
            "Invoke api pygame-surface-pixel with arguments (screen, [xpos + rectWidth, ypos]) saving result as outside then print outside.r + outside.g + outside.b. Quit Pygame.",
            $"{expected}\n0\n");
    }

    [Fact]
    public async Task Executes_multiple_distinct_rectangles_in_one_sentence()
    {
        await GenerateAndExecute(
            "Write a Python program using Pygame. Initialize Pygame. Open a window that is 64 wide and 48 tall. Clear the screen to black. " +
            "Draw the first as a red rectangle at x 2 and y 3 with width 4 and height 5 then draw the second as a white rectangle at x 20 and y 30 with width 6 and height 7. " +
            "Invoke api pygame-surface-pixel with arguments (screen, [2, 3]) saving result as firstPixel then print firstPixel.r + firstPixel.g + firstPixel.b. " +
            "Invoke api pygame-surface-pixel with arguments (screen, [20, 30]) saving result as secondPixel then print secondPixel.r + secondPixel.g + secondPixel.b. Quit Pygame.",
            "255\n765\n");
    }

    [Theory]
    [InlineData("red", 20, 24, 4, 255)]
    [InlineData("white", 30, 20, 7, 765)]
    public async Task Executes_mixed_circle_and_rectangle_descriptions(string color, int x, int y, int radius, int expected)
    {
        await GenerateAndExecute(
            "Write a Python program using Pygame. Initialize Pygame. Open a window that is 64 wide and 48 tall. Clear the screen to black. " +
            $"Set xpos to {x} and set ypos to {y} and set size to {radius}. " +
            $"Draw marker as a {color} circle at x xpos and y ypos with radius size then draw panel as a white rectangle at x 2 and y 3 with width 4 and height 5. " +
            "Invoke api pygame-surface-pixel with arguments (screen, [xpos, ypos]) saving result as center then print center.r + center.g + center.b. " +
            "Invoke api pygame-surface-pixel with arguments (screen, [xpos + size + 1, ypos]) saving result as outside then print outside.r + outside.g + outside.b. " +
            "Invoke api pygame-surface-pixel with arguments (screen, [2, 3]) saving result as panel then print panel.r + panel.g + panel.b. Quit Pygame.",
            $"{expected}\n0\n765\n");
    }

    [Theory]
    [InlineData("keyboard state", "W", "keys", "code")]
    [InlineData("pressed keys", "S", "state", "selected")]
    [InlineData("keyboard state", "up arrow", "inputState", "upCode")]
    [InlineData("pressed keys", "down arrow", "held", "downCode")]
    public async Task Executes_natural_keyboard_acquisition_and_indexed_control_checks(
        string wording, string key, string state, string codeName)
    {
        await GenerateAndExecute(
            "Write a Python program using Pygame. Initialize Pygame. Open a window that is 32 wide and 24 tall. " +
            $"Read {wording} saving result as {state}. Read the {key} key code saving result as {codeName}. " +
            $"When {state}[{codeName}] == True then print 'held' otherwise print 'released'. Quit Pygame.",
            "released\n");
    }

    [Theory]
    [InlineData("pending", "notifications", "closeKind")]
    [InlineData("queued", "queue", "stopCode")]
    public async Task Executes_injected_close_event_stopping_an_outer_request_derived_loop(
        string wording, string queue, string kind)
    {
        var code = await GenerateAndExecute(
            "Write a Python program using Pygame. Initialize Pygame. Open a window that is 32 wide and 24 tall. " +
            $"Read window close event code saving result as {kind}. " +
            $"Invoke api pygame-event with arguments ({kind}) saving result as closing. Invoke api pygame-post-event with arguments (closing). " +
            "Set active to 1 and set frames to 0 then while active is greater than 0 then " +
            $"read {wording} events saving result as {queue}. For each notification in {queue} then " +
            $"when notification.type is equal to {kind} then set active to 0 then leave repetition. End repetition. " +
            "Set frames to frames + 1. End repetition. Quit Pygame. Print frames then print active.",
            "1\n0\n");
        Assert.Contains("            break", code, StringComparison.Ordinal);
        Assert.Contains("pygame.quit()\nprint(frames)", code, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Executes_default_event_bindings_and_an_empty_second_read()
    {
        await GenerateAndExecute(
            "Write a Python program using Pygame. Initialize Pygame. Open a window that is 32 wide and 24 tall. Read queued events. " +
            "Read window close event code. Invoke api pygame-event with arguments (close_code) saving result as closing. Invoke api pygame-post-event with arguments (closing). " +
            "Read pending events. Set seen to 0 then for each notification in events then when notification.type is equal to close_code then set seen to seen + 1. End repetition. " +
            "Read queued events then for each notification in events then set seen to seen + 10. End repetition. Print seen. Quit Pygame.",
            "1\n");
    }

    private static async Task<string> GenerateAndExecute(string request, string expected)
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
        var code = string.Join('\n', lines[(start + 1)..end]);
        await Execute(code, expected);
        return code;
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
