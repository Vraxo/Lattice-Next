using System.Text.Json;
using Xunit;
using Lattice.Cli;

namespace Lattice.Cli.Tests;

public sealed class LatticeCliTests
{
    [Fact]
    public async Task Emits_json_by_default_and_returns_success_for_a_handled_turn()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await LatticeCli.RunAsync(
            ["Fido is a dog. Is Fido a dog?"],
            new StringReader(string.Empty),
            output,
            error,
            standardInputIsRedirected: false);

        using var document = JsonDocument.Parse(output.ToString());
        var root = document.RootElement;

        Assert.Equal(0, exitCode);
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(
            "Completed",
            root.GetProperty("turn").GetProperty("status").GetString());
        Assert.Equal(2, root.GetProperty("turn").GetProperty("utterances").GetArrayLength());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task Loads_knowledge_packs_before_submitting_the_turn()
    {
        var packPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(
            packPath,
            """{"formatVersion":1,"id":"animals","name":"Animals","claims":["Fido is a dog."]}""");

        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = await LatticeCli.RunAsync(
                ["--load-pack", packPath, "Is Fido a dog?"],
                new StringReader(string.Empty),
                output,
                error,
                standardInputIsRedirected: false);

            using var document = JsonDocument.Parse(output.ToString());
            var root = document.RootElement;
            var answer = root.GetProperty("turn").GetProperty("utterances")[0];

            Assert.Equal(0, exitCode);
            Assert.Equal("Animals", root.GetProperty("loadedPacks")[0].GetProperty("name").GetString());
            Assert.Equal("True", answer.GetProperty("truthValue").GetString());
            Assert.Equal(
                "animals",
                answer.GetProperty("supportingEvidence")[0].GetProperty("sources")[0].GetProperty("sourceId").GetString());
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            File.Delete(packPath);
        }
    }

    [Fact]
    public async Task Reads_a_turn_from_redirected_standard_input()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await LatticeCli.RunAsync(
            [],
            new StringReader("Fido is a dog."),
            output,
            error,
            standardInputIsRedirected: true);

        using var document = JsonDocument.Parse(output.ToString());

        Assert.Equal(0, exitCode);
        Assert.True(document.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task Returns_a_usage_exit_code_for_unknown_options()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await LatticeCli.RunAsync(
            ["--unknown"],
            new StringReader(string.Empty),
            output,
            error,
            standardInputIsRedirected: false);

        Assert.Equal(64, exitCode);
        Assert.Contains("Unknown option '--unknown'.", error.ToString());
        Assert.Equal(string.Empty, output.ToString());
    }
    [Fact]
    public async Task Returns_a_processing_exit_code_and_json_error_for_rejected_input()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await LatticeCli.RunAsync(
            ["unsupported input"],
            new StringReader(string.Empty),
            output,
            error,
            standardInputIsRedirected: false);

        using var document = JsonDocument.Parse(output.ToString());
        var root = document.RootElement;

        Assert.Equal(2, exitCode);
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("error").GetString()));
        Assert.Equal(string.Empty, error.ToString());
    }

}
