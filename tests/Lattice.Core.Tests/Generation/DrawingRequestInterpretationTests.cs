using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class DrawingRequestInterpretationTests
{
    [Theory]
    [InlineData("left paddle", "white", "20", "210", "10", "60")]
    [InlineData("marker", "red", "2 + 3", "4", "8 * 2", "9")]
    public void Rectangle_descriptions_carry_all_request_derived_arguments(
        string label, string color, string x, string y, string width, string height)
    {
        var request = $"Write a Python program using Pygame. Open a window that is 640 wide and 480 tall. Draw the {label} as a {color} rectangle at x {x} and y {y} with width {width} and height {height}.";
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var error), error);
        Assert.True(SequenceInputFactExtractor.TryExtractProgram(knowledge!, request, out var program, out error), error);
        var call = Assert.IsType<CallRequirement>(program!.Requirements[2]);
        Assert.Equal("pygame-draw-rectangle-dimensions", call.ApiContract);
        Assert.Equal(new SemanticNameExpression("screen"), call.Arguments[0]);
        Assert.Equal(new SemanticTextExpression(color), call.Arguments[1]);
        Assert.Equal(6, call.Arguments.Length);
        Assert.True(new GenerationLanguagePackLoader().TryLoadEmbedded(out var packs, out error), error);
        Assert.True(SequenceInputFactExtractor.TryExtract(knowledge!, request, out var facts, out error), error);
        Assert.True(SemanticProgramPlanner.TryGenerateProgram(facts, packs["python"], out var code, out _, out error), error);
        Assert.Contains($"pygame.draw.rect(screen, '{color}', [{x}, {y}, {width}, {height}])", code, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("missing", "4", "8", "9")]
    [InlineData("3", "missing", "8", "9")]
    [InlineData("3", "4", "missing", "9")]
    [InlineData("3", "4", "8", "missing")]
    public void Rectangle_arguments_must_reference_available_state(string x, string y, string width, string height)
    {
        var request = $"Write a Python program using Pygame. Open a window that is 32 wide and 24 tall. Draw a marker as a red rectangle at x {x} and y {y} with width {width} and height {height}.";
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var error), error);
        Assert.True(SequenceInputFactExtractor.TryExtract(knowledge!, request, out var facts, out error), error);
        Assert.True(new GenerationLanguagePackLoader().TryLoadEmbedded(out var packs, out error), error);
        Assert.False(SemanticProgramPlanner.TryGenerateProgram(facts, packs["python"], out var code, out _, out _));
        Assert.Empty(code);
    }
}
