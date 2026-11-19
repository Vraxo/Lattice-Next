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
}
