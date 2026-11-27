using System.Diagnostics;
using Lattice.Client;
using Lattice.Runtime;
using Xunit;

namespace Lattice.Runtime.Tests;

public sealed class WhileProgramGenerationTests
{
    [Theory]
    [InlineData("count", "0", "less than", "3", "+", "1", "0\n1\n2\n")]
    [InlineData("position", "2", "less than", "9", "+", "2", "2\n4\n6\n8\n")]
    [InlineData("remaining", "6", "greater than", "0", "-", "2", "6\n4\n2\n")]
    [InlineData("skipped", "5", "less than", "3", "+", "1", "")]
    public async Task Generates_and_executes_a_request_derived_loop(
        string name, string initial, string comparison, string limit, string operation, string step, string expected)
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();
        var request = $"Write a Python program that set {name} to {initial} then while {name} is {comparison} {limit} " +
            $"then print {name} then set {name} to {name} {operation} {step}.";
        var result = await session.SubmitAsync(request);
        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, utterance.Kind);
        var fence = new string('`', 3);
        var lines = utterance.Message!.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var start = Array.IndexOf(lines, fence + "python");
        var end = Array.IndexOf(lines, fence, start + 1);
        Assert.True(start >= 0 && end > start);
        var code = string.Join('\n', lines[(start + 1)..end]);
        var symbol = comparison == "less than" ? "<" : ">";
        Assert.Equal($"{name} = {initial}\nwhile {name} {symbol} {limit}:\n    print({name})\n    {name} = {name} {operation} {step}", code);
        await Execute(code, expected);
    }

    [Fact]
    public async Task Executes_multiple_body_statements_in_source_order()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();
        var result = await session.SubmitAsync(
            "Create a Python program that set total to 0 then while total is less than 3 then set doubled to total * 2 then print doubled then set total to total + 1.");
        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, utterance.Kind);
        var fence = new string('`', 3);
        var text = utterance.Message!.Replace("\r\n", "\n", StringComparison.Ordinal);
        var start = text.IndexOf(fence + "python\n", StringComparison.Ordinal);
        Assert.True(start >= 0);
        start += fence.Length + "python\n".Length;
        var end = text.IndexOf(fence, start, StringComparison.Ordinal);
        Assert.True(end > start);
        await Execute(text[start..end].TrimEnd(), "0\n2\n4\n");
    }

    private static async Task Execute(string code, string expected)
    {
        var info = new ProcessStartInfo(OperatingSystem.IsWindows() ? "python" : "python3")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
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
