using Xunit;

namespace Lattice.Runtime.Tests;

// Temporary probe: always fails, to report what the runtime returns for a hello-world request.
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

        var detail = result.Utterances.IsDefaultOrEmpty
            ? $"status={result.Status}; error={result.Error}; count=0"
            : $"status={result.Status}; error={result.Error}; count={result.Utterances.Length}; message={result.Utterances[0].Message}";

        Assert.Fail($"PROBE[{input}] {detail}");
    }
}
