using System.Collections.Immutable;
using System.Text.Json;
using Lattice.Core.Tools;

namespace Lattice.Core.Tests.Tools;

public sealed class RegistryEchoTool : ITool
{
    public RegistryEchoTool(
        string id = "number-echo",
        ToolPermission requiredPermissions = ToolPermission.None)
    {
        Descriptor = new ToolDescriptor(
            id,
            "Number echo",
            "Returns the supplied numeric value.",
            ImmutableArray.Create(
                new ToolInputDescriptor("value", "The numeric value to return.", ToolInputType.Number)),
            requiredPermissions);
    }

    public ToolDescriptor Descriptor { get; }

    public ValueTask<ToolExecutionResult> ExecuteAsync(
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(
            ToolExecutionResult.Success(arguments.GetProperty("value").GetRawText()));
    }
}
