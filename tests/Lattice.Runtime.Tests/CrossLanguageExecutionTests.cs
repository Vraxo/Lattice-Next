using System.Collections.Immutable;
using System.Diagnostics;
using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Runtime.Tests;

/// <summary>
/// Runs the two languages' programs for the same request and compares what they report. The outputs are compared
/// after removing insignificant whitespace, because each language prints a list in its own style.
/// </summary>
public sealed class CrossLanguageExecutionTests
{
    private static readonly ImmutableDictionary<string, GenerationLanguagePack> Packs = LoadPacks();

    [Theory]
    [InlineData("Create a Python program that sets total to 7 then print total.", "7")]
    [InlineData(
        "Build a Python function named keep_large that accepts a list of numbers named samples and returns a new list containing only sample values more than 4.",
        "[5]")]
    [InlineData(
        "Write a Python program that fulfills this request: Given a dictionary of product names and prices, print each product with its price.",
        "")]
    public async Task Reports_the_same_result_in_each_language(string request, string expected)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        Assert.True(
            SequenceInputFactExtractor.TryExtract(knowledge!, request, out var facts, out var extractionError),
            extractionError);

        Assert.True(
            SemanticProgramPlanner.TryGenerateProgram(
                facts, Packs["python"], out var pythonCode, out _, out var pythonError),
            pythonError);
        Assert.True(
            SemanticProgramPlanner.TryGenerateProgram(
                facts, Packs["javascript"], out var javaScriptCode, out _, out var javaScriptError),
            javaScriptError);

        var pythonOutput = Normalize(await RunAsync(OperatingSystem.IsWindows() ? "python" : "python3", ["-c", pythonCode]));
        var javaScriptOutput = Normalize(await RunAsync("node", ["-e", javaScriptCode]));

        Assert.Equal(expected, pythonOutput);
        Assert.Equal(expected, javaScriptOutput);
    }

    private static string Normalize(string output) =>
        string.Concat(output.Where(character => !char.IsWhiteSpace(character)));

    private static async Task<string> RunAsync(string fileName, string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        var outputTask = process!.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = await outputTask;
        var error = await errorTask;

        Assert.Equal(0, process.ExitCode);
        Assert.True(error.Length == 0, error);
        return output.TrimEnd();
    }

    private static ImmutableDictionary<string, GenerationLanguagePack> LoadPacks()
    {
        var loader = new GenerationLanguagePackLoader();
        Assert.True(loader.TryLoadEmbedded(out var packs, out var error), error);
        return packs;
    }
}
