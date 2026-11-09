using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;

namespace Lattice.Core.Tools;

public sealed class UnitConverterTool : ITool
{
    private static readonly ToolDescriptor ToolDescriptor = new(
        "unit-converter",
        "Unit converter",
        "Converts values between supported units of the same dimension.",
        ImmutableArray.Create(
            new ToolInputDescriptor("value", "The numeric value to convert.", ToolInputType.Number),
            new ToolInputDescriptor("from", "The source unit.", ToolInputType.String),
            new ToolInputDescriptor("to", "The destination unit.", ToolInputType.String)));

    private static readonly IReadOnlyDictionary<string, (string Dimension, decimal Factor)> Units =
        new Dictionary<string, (string Dimension, decimal Factor)>(StringComparer.OrdinalIgnoreCase)
        {
            ["mm"] = ("length", 0.001m),
            ["cm"] = ("length", 0.01m),
            ["m"] = ("length", 1m),
            ["km"] = ("length", 1000m),
            ["mg"] = ("mass", 0.001m),
            ["g"] = ("mass", 1m),
            ["kg"] = ("mass", 1000m),
            ["ml"] = ("volume", 0.001m),
            ["l"] = ("volume", 1m),
            ["s"] = ("time", 1m),
            ["min"] = ("time", 60m),
            ["h"] = ("time", 3600m)
        };

    public ToolDescriptor Descriptor => ToolDescriptor;

    public ValueTask<ToolExecutionResult> ExecuteAsync(
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var value = arguments.GetProperty("value").GetDecimal();
        var fromName = arguments.GetProperty("from").GetString();
        var toName = arguments.GetProperty("to").GetString();

        if (fromName is null || !Units.TryGetValue(fromName, out var from))
        {
            return ValueTask.FromResult(
                ToolExecutionResult.Failure($"Unsupported source unit '{fromName}'."));
        }

        if (toName is null || !Units.TryGetValue(toName, out var to))
        {
            return ValueTask.FromResult(
                ToolExecutionResult.Failure($"Unsupported destination unit '{toName}'."));
        }

        if (!string.Equals(from.Dimension, to.Dimension, StringComparison.Ordinal))
        {
            return ValueTask.FromResult(
                ToolExecutionResult.Failure("The source and destination units have incompatible dimensions."));
        }

        try
        {
            var converted = value * from.Factor / to.Factor;
            return ValueTask.FromResult(
                ToolExecutionResult.Success(
                    converted.ToString("0.############################", CultureInfo.InvariantCulture)));
        }
        catch (OverflowException)
        {
            return ValueTask.FromResult(
                ToolExecutionResult.Failure("The converted value is outside the supported decimal range."));
        }
    }
}
