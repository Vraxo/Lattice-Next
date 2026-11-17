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
    [InlineData(
        "Create a Python program that prints each number from 1 to 5.",
        "for number in range(1, 6):\n    print(number)",
        "1\n2\n3\n4\n5")]
    [InlineData(
        "Build a Python script that displays each value from 2 through 4.",
        "for value in range(2, 5):\n    print(value)",
        "2\n3\n4")]
    [InlineData(
        "Generate a Python program that outputs each count from 3 to 6.",
        "for count in range(3, 7):\n    print(count)",
        "3\n4\n5\n6")]
    [InlineData(
        "Create a Python program with a function named double that takes value and returns value * 2, then print double(4).",
        "def double(value):\n    return value * 2\nprint(double(4))",
        "8")]
    [InlineData(
        "Build a Python script with a function named triple that takes number and returns number * 3, then print triple(3).",
        "def triple(number):\n    return number * 3\nprint(triple(3))",
        "9")]
    [InlineData(
        "Generate a Python program with a function named add_five that takes value and returns value + 5, then print add_five(7).",
        "def add_five(value):\n    return value + 5\nprint(add_five(7))",
        "12")]
    [InlineData(
        "Create a Python program with a function named add that takes left and right and returns left + right, then print add(3, 5).",
        "def add(left, right):\n    return left + right\nprint(add(3, 5))",
        "8")]
    [InlineData(
        "Build a Python script with a function named multiply that takes width and height and returns width * height, then print multiply(6, 7).",
        "def multiply(width, height):\n    return width * height\nprint(multiply(6, 7))",
        "42")]
    [InlineData(
        "Generate a Python program with a function named subtract that takes larger and smaller and returns larger - smaller, then print subtract(15, 4).",
        "def subtract(larger, smaller):\n    return larger - smaller\nprint(subtract(15, 4))",
        "11")]
    [InlineData(
        "Create a Python program with a function named sum_three that takes first and second and third and returns first + second + third, then print sum_three(2, 3, 4).",
        "def sum_three(first, second, third):\n    return first + second + third\nprint(sum_three(2, 3, 4))",
        "9")]
    [InlineData(
        "Build a Python script with a function named volume that takes width and height and depth and returns width * height * depth, then print volume(2, 3, 5).",
        "def volume(width, height, depth):\n    return width * height * depth\nprint(volume(2, 3, 5))",
        "30")]
    [InlineData(
        "Generate a Python program with a function named weighted_total that takes base and bonus and penalty and returns base + bonus - penalty, then print weighted_total(20, 7, 3).",
        "def weighted_total(base, bonus, penalty):\n    return base + bonus - penalty\nprint(weighted_total(20, 7, 3))",
        "24")]
    [InlineData(
        "Create a Python program with a function named scale that takes value and factor and returns (value + 3) * factor, then print scale(2, 4).",
        "def scale(value, factor):\n    return (value + 3) * factor\nprint(scale(2, 4))",
        "20")]
    [InlineData(
        "Build a Python script with a function named weighted that takes base and bonus and factor and returns (base + bonus) * (factor - 1), then print weighted(5, 2, 4).",
        "def weighted(base, bonus, factor):\n    return (base + bonus) * (factor - 1)\nprint(weighted(5, 2, 4))",
        "21")]
    [InlineData(
        "Create a Python program with a function named clamp_floor that takes value and returns max(abs(value), 10) + 2, then print clamp_floor(-4).",
        "def clamp_floor(value):\n    return max(abs(value), 10) + 2\nprint(clamp_floor(-4))",
        "12")]
    [InlineData(
        "Build a Python script with a function named rounded_ratio that takes total and count and returns round(total / count, 2), then print rounded_ratio(10, 3).",
        "def rounded_ratio(total, count):\n    return round(total / count, 2)\nprint(rounded_ratio(10, 3))",
        "3.33")]
    [InlineData(
        "Generate a Python program that sets total to 5 and prints (total + 3) * 2.",
        "total = 5\nprint((total + 3) * 2)",
        "16")]
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
        Assert.Equal(expectedOutput.Replace("\n", Environment.NewLine, StringComparison.Ordinal) + Environment.NewLine, output);
    }
}
