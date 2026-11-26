using Xunit;

namespace Lattice.Runtime.Tests;

// Temporary probe: reports the runtime turn status and error for a generation request.
public sealed class RuntimePathProbeTests
{
    [Theory]
    [InlineData("write a hello world program in python")]
    [InlineData("Write a Hello World program in Python.")]
    [InlineData("Create a Python program that prints \"Hello, world!\".")]
    public async Task Probe(string input)
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(input);

        Assert.True(
            result.Utterances.Length == 1,
            $"status={result.Status}; error={result.Error}; count={result.Utterances.Length}");
    }
}
