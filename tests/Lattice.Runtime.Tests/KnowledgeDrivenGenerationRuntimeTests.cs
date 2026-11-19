using Lattice.Client;
using Lattice.Runtime;
using Xunit;

namespace Lattice.Runtime.Tests;

public sealed class KnowledgeDrivenGenerationRuntimeTests
{
    [Fact]
    public async Task Generates_from_multiple_semantic_facts_before_legacy_request_parsing()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Create a Python program that prints \"Hello, world!\".");

        var response = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, response.Kind);
        Assert.Equal("```python\nprint(\"Hello, world!\")\n```", response.Message);
        Assert.Contains("language.python", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("intent.program-generation", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("value.output", response.Meaning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generates_hello_world_from_its_semantic_output_requirement()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Write a Hello World program in Python.");

        var response = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, response.Kind);
        Assert.Equal("\u0060\u0060\u0060python\nprint(\"Hello, world!\")\n\u0060\u0060\u0060", response.Message);
        Assert.Contains("program.output.start", response.Meaning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Composes_assignment_and_output_in_one_runtime_request()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Create a Python program that sets total to 5 and prints 'Done'.");

        var response = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, response.Kind);
        Assert.Equal("\u0060\u0060\u0060python\ntotal = 5\nprint(\"Done\")\n\u0060\u0060\u0060", response.Message);
        Assert.Contains("program.assignment.target", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("program.output", response.Meaning, StringComparison.Ordinal);
    }

}
