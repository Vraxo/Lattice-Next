using System.Text.Json;
using Lattice.Core.Intelligence;
using Lattice.Core.Tools;
using Xunit;

namespace Lattice.Core.Tests.Intelligence;

public sealed class UnitConversionIntentRecognizerTests
{
    private readonly UnitConversionIntentRecognizer recognizer = new();

    [Theory]
    [InlineData("Convert 150 cm to meters.", 150, "cm", "m")]
    [InlineData("How many meters is 150 cm?", 150, "cm", "m")]
    [InlineData("2 hours in minutes", 2, "h", "min")]
    public void Recognizes_unit_conversion_requests(
        string input,
        decimal expectedValue,
        string expectedFrom,
        string expectedTo)
    {
        var recognized = recognizer.TryRecognize(input, null, out var intent);

        Assert.True(recognized);
        var actual = Assert.IsType<ToolIntent>(intent);
        Assert.Equal("unit-converter", actual.ToolId);

        using var arguments = JsonDocument.Parse(actual.ArgumentsJson);
        Assert.Equal(expectedValue, arguments.RootElement.GetProperty("value").GetDecimal());
        Assert.Equal(expectedFrom, arguments.RootElement.GetProperty("from").GetString());
        Assert.Equal(expectedTo, arguments.RootElement.GetProperty("to").GetString());
    }

    [Fact]
    public void Formats_a_conversational_conversion_response()
    {
        recognizer.TryRecognize("Convert 150 cm to meters.", null, out var intent);

        var response = Assert.IsType<ToolIntent>(intent)
            .FormatResponse(new ToolObservation("unit-converter", "1.5"));

        Assert.Equal("150 cm is 1.5 m.", response);
    }

    [Fact]
    public void Does_not_recognize_unsupported_unit_names()
    {
        var recognized = recognizer.TryRecognize("Convert 1 parsec to meters.", null, out var intent);

        Assert.False(recognized);
        Assert.Null(intent);
    }
}
