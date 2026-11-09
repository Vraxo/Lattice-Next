using System.Collections.Immutable;
using System.Text.Json;
using Lattice.Core.Tools;

namespace Lattice.Core.Tests.Tools;

public sealed class RegistryEchoTool : ITool
{
    private static readonly ToolDescriptor ToolDescriptor = new(
        "number-echo",
        "Number echo",
        "Returns the supplied numeric value.",
        ImmutableArray.Create(
            new ToolInputDescriptor("value", "The numeric value to return.", ToolInputType.Number)));

    public ToolDescriptor Descriptor => ToolDescriptor;

    public ValueTask<ToolExecutionResult> ExecuteAsync(
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(
            ToolExecutionResult.Success(arguments.GetProperty("value").GetRawText()));
    }
}
