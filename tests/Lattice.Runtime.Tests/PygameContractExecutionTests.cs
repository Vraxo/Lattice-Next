using System.Diagnostics;
using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Runtime.Tests;

/// <summary>Library-contract execution, not request-to-game acceptance.</summary>
public sealed class PygameContractExecutionTests
{
    [Theory]
    [InlineData("surface", "timer", 160, 120)]
    [InlineData("canvas", "pacer", 240, 180)]
    public async Task Executes_composed_window_input_drawing_and_timing_contracts(
        string surface, string timer, int width, int height)
    {
        SemanticExpression screen = new SemanticNameExpression(surface);
        SemanticExpression white = Numbers(255, 255, 255);
        var program = new SemanticProgram([
            new ImportRequirement("pygame"),
            Bind("initialized", Api("pygame-init")),
            Bind(surface, Api("pygame-window", Numbers(width, height))),
            Bind(timer, Api("pygame-clock")),
            Bind("cleared", Api("pygame-surface-fill", screen, Numbers(0, 0, 0))),
            Bind("rectangle", Api("pygame-draw-rectangle", screen, white, Numbers(10, 10, 20, 30))),
            Bind("circle", Api("pygame-draw-circle", screen, white, Numbers(60, 60), Number(5))),
            Bind("line", Api("pygame-draw-line", screen, white, Numbers(80, 80), Numbers(90, 90), Number(1))),
            Bind("presented", Api("pygame-present")),
            new OutputRequirement(Api("pygame-surface-size", screen)),
            new OutputRequirement(new SemanticBinaryExpression(SemanticExpressionOperator.Equal,
                new SemanticSubscriptExpression(Api("pygame-surface-pixel", screen, Numbers(10, 10)), Number(0)), Number(255))),
            Bind("seconds", new SemanticBinaryExpression(SemanticExpressionOperator.Multiply,
                Api("pygame-tick", new SemanticNameExpression(timer), Number(0)), new SemanticNumberExpression("0.001"))),
            new OutputRequirement(new SemanticBinaryExpression(SemanticExpressionOperator.GreaterThanOrEqual,
                new SemanticNameExpression("seconds"), Number(0))),
            Bind("keys", Api("pygame-keys")),
            new OutputRequirement(new SemanticListLiteralExpression([
                new SemanticSubscriptExpression(new SemanticNameExpression("keys"), Api("pygame-key-w")),
                new SemanticSubscriptExpression(new SemanticNameExpression("keys"), Api("pygame-key-s")),
                new SemanticSubscriptExpression(new SemanticNameExpression("keys"), Api("pygame-key-up")),
                new SemanticSubscriptExpression(new SemanticNameExpression("keys"), Api("pygame-key-down"))])),
            Bind("posted", Api("pygame-post-event", Api("pygame-event", Api("pygame-quit-event")))),
            new IterationRequirement("event", Api("pygame-events"), [
                new ConditionalRequirement(new SemanticBinaryExpression(SemanticExpressionOperator.Equal,
                    Api("pygame-event-type", new SemanticNameExpression("event")), Api("pygame-quit-event")),
                    [new OutputRequirement(new SemanticTextExpression("quit observed"))], [])]),
            Bind("closed", Api("pygame-quit"))]);
        Assert.True(SemanticProgramValidator.IsValid(program));
        Assert.True(new GenerationLanguagePackLoader().TryLoadEmbedded(out var packs, out var error), error);
        Assert.True(SemanticProgramPlanner.TryPlan(program, packs["python"], out var statements, out error), error);
        var code = SemanticProgramPlanner.Render(statements);
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
        Assert.Equal($"({width}, {height})\nTrue\nTrue\n[False, False, False, False]\nquit observed\n",
            (await output).Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    private static AssignmentRequirement Bind(string name, SemanticExpression value) => new(name, value);
    private static SemanticNumberExpression Number(int value) => new(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    private static SemanticListLiteralExpression Numbers(params int[] values) => new([.. values.Select(Number)]);
    private static SemanticApiExpression Api(string contract, params SemanticExpression[] arguments) =>
        new(contract) { Arguments = [.. arguments] };
}
