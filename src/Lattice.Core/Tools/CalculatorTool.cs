using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;

namespace Lattice.Core.Tools;

public sealed class CalculatorTool : ITool
{
    private static readonly ToolDescriptor ToolDescriptor = new(
        "calculator",
        "Calculator",
        "Performs one exact decimal arithmetic operation.",
        ImmutableArray.Create(
            new ToolInputDescriptor("left", "The left numeric operand.", ToolInputType.Number),
            new ToolInputDescriptor("operation", "One of add, subtract, multiply, or divide.", ToolInputType.String),
            new ToolInputDescriptor("right", "The right numeric operand.", ToolInputType.Number)));

    public ToolDescriptor Descriptor => ToolDescriptor;

    public ValueTask<ToolExecutionResult> ExecuteAsync(
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var left = arguments.GetProperty("left").GetDecimal();
        var operation = arguments.GetProperty("operation").GetString();
        var right = arguments.GetProperty("right").GetDecimal();

        if (right == 0 && string.Equals(operation, "divide", StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult(
                ToolExecutionResult.Failure("Division by zero is undefined."));
        }

        try
        {
            var result = operation?.ToLowerInvariant() switch
            {
                "add" => left + right,
                "subtract" => left - right,
                "multiply" => left * right,
                "divide" => left / right,
                _ => (decimal?)null
            };

            if (result is null)
            {
                return ValueTask.FromResult(
                    ToolExecutionResult.Failure(
                        "Operation must be add, subtract, multiply, or divide."));
            }

            var output = result.Value.ToString(CultureInfo.InvariantCulture);
            return ValueTask.FromResult(ToolExecutionResult.Success(output));
        }
        catch (OverflowException)
        {
            return ValueTask.FromResult(
                ToolExecutionResult.Failure("The result is outside the supported decimal range."));
        }
    }
}
