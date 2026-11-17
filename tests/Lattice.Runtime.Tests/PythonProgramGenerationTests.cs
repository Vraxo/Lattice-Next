using System.Diagnostics;
using Lattice.Client;
using Lattice.Runtime;
using Xunit;

namespace Lattice.Runtime.Tests;

public sealed class PythonProgramGenerationTests
{
    [Theory]
    [InlineData(
        "Create a Python program that sets total to 5 and prints total + 3.",
        "total = 5\nprint(total + 3)",
        "8")]
    [InlineData(
        "Build a Python script that sets count to 12 and prints count * 2.",
        "count = 12\nprint(count * 2)",
        "24")]
    [InlineData(
        "Generate a Python program that sets result to 9 and prints result - 4.",
        "result = 9\nprint(result - 4)",
        "5")]
    [InlineData(
        "Create a Python program that prints 'adult' if 20 is at least 18.",
        "if 20 >= 18:\n    print(\"adult\")",
        "adult")]
    [InlineData(
        "Build a Python script that displays 'minor' if 16 is less than 18.",
        "if 16 < 18:\n    print(\"minor\")",
        "minor")]
    [InlineData(
        "Generate a Python program that outputs 'match' if 7 equals 7.",
        "if 7 == 7:\n    print(\"match\")",
        "match")]
    public async Task Generates_and_executes_composed_python_programs(
        string input,
        string expectedCode,
        string expectedOutput)
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(input);

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, utterance.Kind);

        var fence = new string((char)96, 3);
        var lines = utterance.Message.Split(Environment.NewLine, StringSplitOptions.None);
        var start = Array.IndexOf(lines, fence + "python");
        var end = Array.IndexOf(lines, fence, start + 1);
        Assert.True(start >= 0 && end > start, utterance.Message);

        var generatedCode = string.Join(Environment.NewLine, lines[(start + 1)..end]);
        Assert.Equal(expectedCode.Replace("\n", Environment.NewLine, StringComparison.Ordinal), generatedCode);
        await AssertPythonOutput(generatedCode, expectedOutput);
    }

    private static async Task AssertPythonOutput(string code, string expectedOutput)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "python" : "python3",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(code);

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        var outputTask = process!.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var output = await outputTask;
        var error = await errorTask;
        Assert.True(process.ExitCode == 0, error);
        Assert.Equal(expectedOutput + Environment.NewLine, output);
    }
}
