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

    [Theory]
    [InlineData(
        "Write a Python program that set count to 0 then while count is less than 4 then set count to count + 1. When count is equal to 2 then print 'two'. When count is equal to 4 then print 'four'.",
        "two\nfour\n")]
    [InlineData(
        "Create a Python program that set position to 1 then while position is less than 8 then set position to position + 2. When position is equal to 3 then print 'three'. When position is at least 7 then print position.",
        "three\n7\n9\n")]
    [InlineData(
        "Write a Python program that set amount to 2 then when amount is greater than 0 then when amount is less than 5 then print 'inside'. Set amount to 8 then print amount.",
        "inside\n8\n")]
    [InlineData(
        "Write a Python program that set amount to 0 then when amount is greater than 0 then print 'wrong'. Set amount to 7 then print amount.",
        "7\n")]
    public async Task Executes_request_derived_sentence_scoped_conditions(string request, string expected)
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();
        var result = await session.SubmitAsync(request);
        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, utterance.Kind);
        var fence = new string('`', 3);
        var lines = utterance.Message!.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var start = Array.IndexOf(lines, fence + "python");
        var end = Array.IndexOf(lines, fence, start + 1);
        Assert.True(start >= 0 && end > start);
        await Execute(string.Join('\n', lines[(start + 1)..end]), expected);
    }

    [Theory]
    [InlineData(
        "Write a Python program that set round to 0 then set text to 'ab' then while round is less than 2 then set round to round + 1. For each letter in text then when letter is equal to 'b' then print letter. End repetition. Print round.",
        "b\n1\nb\n2\n")]
    [InlineData(
        "Write a Python program that set outer to 0 then while outer is less than 2 then set inner to 0. While inner is less than 2 then print inner then set inner to inner + 1. End repetition. Set outer to outer + 1. End repetition. Print outer.",
        "0\n1\n0\n1\n2\n")]
    [InlineData(
        "Create a Python program that set text to 'ab' then for each letter in text.upper() then print letter. End repetition. Print text[0].",
        "A\nB\na\n")]
    [InlineData(
        "Write a Python program that set limit to 3 then for each number in range(0, limit) then print number. End repetition. Print limit.",
        "0\n1\n2\n3\n")]
    public async Task Executes_nested_and_explicitly_bounded_repetitions(string request, string expected)
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();
        var result = await session.SubmitAsync(request);
        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, utterance.Kind);
        var fence = new string('`', 3);
        var lines = utterance.Message!.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var start = Array.IndexOf(lines, fence + "python");
        var end = Array.IndexOf(lines, fence, start + 1);
        Assert.True(start >= 0 && end > start);
        await Execute(string.Join('\n', lines[(start + 1)..end]), expected);
    }

    [Theory]
    [InlineData(
        "Write a Python program that set counter to 0 then set total to 0 then while counter is less than 3 then when counter is less than 2 then set total to total + 2. Set counter to counter + 1. End repetition. Print total.",
        "4\n")]
    [InlineData(
        "Write a Python program that set frame to 0 then set position to 5 then while frame is less than 4 then when frame is less than 3 then set position to position + 3. Set frame to frame + 1. End repetition. Print position.",
        "14\n")]
    [InlineData(
        "Write a Python program that set letter to 'start' then set text to '' then for each letter in text then print letter. End repetition. Print letter.",
        "start\n")]
    [InlineData(
        "Write a Python program that set last to '' then set text to 'ab' then for each letter in text then set last to letter. End repetition. Print last.",
        "b\n")]
    public async Task Preserves_prebound_state_across_conditional_and_repetition_bodies(string request, string expected)
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
