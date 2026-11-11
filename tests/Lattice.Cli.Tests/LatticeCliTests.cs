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
    public async Task Writes_objective_responses_in_text_mode()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        const string input = "I'm looking for a laptop that will last a long time. I don't need gaming performance, but I care about screen quality, reliability, and repairability. Help me compare these three options. My budget is $1,000.";

        var exitCode = await LatticeCli.RunAsync(
            ["--format", "text", input],
            new StringReader(string.Empty),
            output,
            error,
            standardInputIsRedirected: false);

        Assert.Equal(0, exitCode);
        Assert.Contains("I've retained this objective:", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task Treats_objective_clarification_as_a_successful_text_response()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await LatticeCli.RunAsync(
            ["--format", "text", "I have a free Saturday, a limited budget, and no car. I would prefer somewhere quiet and outdoors. Help me put together a good day."],
            new StringReader(string.Empty),
            output,
            error,
            standardInputIsRedirected: false);

        Assert.Equal(0, exitCode);
        Assert.Contains("What is your spending limit?", output.ToString());
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
    public async Task Rejects_server_urls_that_are_not_absolute_http_urls()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await LatticeCli.RunAsync(
            ["--server", "ftp://localhost:5080", "Is Fido a dog?"],
            new StringReader(string.Empty),
            output,
            error,
            standardInputIsRedirected: false);

        Assert.Equal(64, exitCode);
        Assert.Contains("absolute HTTP or HTTPS URL", error.ToString());
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
