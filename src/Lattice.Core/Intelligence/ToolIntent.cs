using Lattice.Core.Tools;

namespace Lattice.Core.Intelligence;

public sealed record ToolIntent(
    string ToolId,
    string ArgumentsJson,
    Func<ToolObservation, string> FormatResponse,
    Func<ToolObservation, ToolIntent?>? Continue = null);
