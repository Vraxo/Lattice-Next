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
    public void Builds_a_sequential_chain_from_each_successful_numeric_observation()
    {
        var observation = new ToolObservation("calculator", "36");

        var recognized = recognizer.TryRecognize(
            "Multiply that by 5, then subtract 10, then divide by 2.",
            observation,
            out var intent);

        Assert.True(recognized);
        var first = Assert.IsType<ToolIntent>(intent);
        AssertInvocation(first, 36m, "multiply", 5m);

        var second = Assert.IsType<ToolIntent>(
            first.Continue!(new ToolObservation("calculator", "180")));
        AssertInvocation(second, 180m, "subtract", 10m);

        var third = Assert.IsType<ToolIntent>(
            second.Continue!(new ToolObservation("calculator", "170")));
        AssertInvocation(third, 170m, "divide", 2m);

        Assert.Null(third.Continue);
        Assert.Equal(
            "170 divided by 2 is 85.",
            third.FormatResponse(new ToolObservation("calculator", "85")));
    }

    [Theory]
    [InlineData("Multiply that by 5, then maybe subtract 10.")]
    [InlineData("Multiply that by 5, then subtract 10 from 3.")]
    [InlineData("Multiply that by 5, then add by 10.")]
    [InlineData("Multiply that by 5, then divide 10 to that.")]
    public void Rejects_a_chain_when_any_step_is_unsupported_or_ambiguous(string input)
    {
        var recognized = recognizer.TryRecognize(
            input,
            new ToolObservation("calculator", "36"),
            out var intent);

        Assert.False(recognized);
        Assert.Null(intent);
    }

    [Fact]
    public void Does_not_guess_a_missing_follow_up_value()
    {
        var recognized = recognizer.TryRecognize("Multiply that by 5.", null, out var intent);

        Assert.False(recognized);
        Assert.Null(intent);
    }

    [Fact]
    public void Does_not_continue_a_chain_from_a_non_numeric_observation()
    {
        var recognized = recognizer.TryRecognize(
            "Multiply that by 5, then subtract 10.",
            new ToolObservation("calculator", "not a number"),
            out var intent);

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

    private static void AssertInvocation(
        ToolIntent intent,
        decimal expectedLeft,
        string expectedOperation,
        decimal expectedRight)
    {
        Assert.Equal("calculator", intent.ToolId);

        using var arguments = JsonDocument.Parse(intent.ArgumentsJson);
        Assert.Equal(expectedLeft, arguments.RootElement.GetProperty("left").GetDecimal());
        Assert.Equal(expectedOperation, arguments.RootElement.GetProperty("operation").GetString());
        Assert.Equal(expectedRight, arguments.RootElement.GetProperty("right").GetDecimal());
    }
}
