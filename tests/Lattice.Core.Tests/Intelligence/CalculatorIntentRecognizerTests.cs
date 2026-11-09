using System.Text.Json;
using Lattice.Core.Intelligence;
using Lattice.Core.Tools;
using Xunit;

namespace Lattice.Core.Tests.Intelligence;

public sealed class CalculatorIntentRecognizerTests
{
    private readonly CalculatorIntentRecognizer recognizer = new();

    [Theory]
    [InlineData("What is 12 multiplied by 3?", 12, "multiply", 3)]
    [InlineData("12 plus 3", 12, "add", 3)]
    [InlineData("Calculate 12 divided by 3", 12, "divide", 3)]
    [InlineData("subtract 3 from 12", 12, "subtract", 3)]
    [InlineData("Multiply 12 by 3", 12, "multiply", 3)]
    public void Recognizes_common_arithmetic_phrasings(
        string input,
        decimal expectedLeft,
        string expectedOperation,
        decimal expectedRight)
    {
        var recognized = recognizer.TryRecognize(input, null, out var intent);

        Assert.True(recognized);
        var actual = Assert.IsType<ToolIntent>(intent);
        Assert.Equal("calculator", actual.ToolId);

        using var arguments = JsonDocument.Parse(actual.ArgumentsJson);
        Assert.Equal(expectedLeft, arguments.RootElement.GetProperty("left").GetDecimal());
        Assert.Equal(expectedOperation, arguments.RootElement.GetProperty("operation").GetString());
        Assert.Equal(expectedRight, arguments.RootElement.GetProperty("right").GetDecimal());
    }

    [Fact]
    public void Resolves_a_follow_up_against_the_latest_numeric_tool_observation()
    {
        var observation = new ToolObservation("calculator", "36");

        var recognized = recognizer.TryRecognize("Multiply that by 5.", observation, out var intent);

        Assert.True(recognized);
        var actual = Assert.IsType<ToolIntent>(intent);

        using var arguments = JsonDocument.Parse(actual.ArgumentsJson);
        Assert.Equal(36m, arguments.RootElement.GetProperty("left").GetDecimal());
        Assert.Equal("multiply", arguments.RootElement.GetProperty("operation").GetString());
        Assert.Equal(5m, arguments.RootElement.GetProperty("right").GetDecimal());
    }

    [Fact]
    public void Does_not_guess_a_missing_follow_up_value()
    {
        var recognized = recognizer.TryRecognize("Multiply that by 5.", null, out var intent);

        Assert.False(recognized);
        Assert.Null(intent);
    }

    [Fact]
    public void Formats_a_response_from_the_sourced_result()
    {
        recognizer.TryRecognize("What is 12 multiplied by 3?", null, out var intent);

        var response = Assert.IsType<ToolIntent>(intent)
            .FormatResponse(new ToolObservation("calculator", "36"));

        Assert.Equal("12 multiplied by 3 is 36.", response);
    }
}
