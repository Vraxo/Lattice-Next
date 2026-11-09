using System.Collections.Immutable;
using System.Text.Json;
using Lattice.Core.Tools;

namespace Lattice.Core.Tests.Tools;

public sealed class BlockingWriteTool : ITool
{
    public ToolDescriptor Descriptor { get; } = new(
        "blocking-write",
        "Blocking write",
        "A test tool that waits until cancelled.",
        ImmutableArray<ToolInputDescriptor>.Empty,
        ToolPermission.Write);

    public async ValueTask<ToolExecutionResult> ExecuteAsync(
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return ToolExecutionResult.Success("unreachable");
    }
}
