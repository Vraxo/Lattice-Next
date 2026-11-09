using System.Text.Json;

namespace Lattice.Core.Tools;

public interface ITool
{
    ToolDescriptor Descriptor { get; }

    ValueTask<ToolExecutionResult> ExecuteAsync(
        JsonElement arguments,
        CancellationToken cancellationToken);
}
