using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class SurfaceRequestInterpretationTests
{
    [Theory]
    [InlineData("the screen", "black", "screen", "black")]
    [InlineData("canvas", "'white'", "canvas", "white")]
    public void Projects_receiver_and_color_onto_the_declared_surface_contract(
        string receiver, string color, string expectedName, string expectedColor)
    {
        var program = Interpret($"Write a Python program using Pygame. Clear {receiver} to {color}. Present the frame.");
        var clear = Assert.IsType<CallRequirement>(program.Requirements[1]);
        Assert.Equal("pygame-surface-fill", clear.ApiContract);
        Assert.Equal(new SemanticNameExpression(expectedName), clear.Arguments[0]);
        Assert.Equal(new SemanticTextExpression(expectedColor), clear.Arguments[1]);
        Assert.Equal("pygame-present", Assert.IsType<CallRequirement>(program.Requirements[2]).ApiContract);
    }

    [Fact]
    public void Text_capture_does_not_turn_ordinary_expression_names_into_strings()
    {
        Assert.True(new GenerationLanguagePackLoader().TryLoadEmbedded(out var packs, out var error), error);
        var syntax = packs["python"].InputSyntax;
        Assert.True(SemanticExpressionParser.TryParse("text", "red", syntax, out var color));
        Assert.Equal(new SemanticTextExpression("red"), color);
        Assert.True(SemanticExpressionParser.TryParse("value", "red", syntax, out var variable));
        Assert.Equal(new SemanticNameExpression("red"), variable);
        Assert.False(SemanticExpressionParser.TryParse("text", "", syntax, out _));
    }

    [Fact]
    public void Multiple_clear_calls_keep_their_own_parameter_values()
    {
        var program = Interpret("Write a Python program using Pygame. Clear the first to red then clear the second to black then present the frame.");
        var first = Assert.IsType<CallRequirement>(program.Requirements[1]);
        var second = Assert.IsType<CallRequirement>(program.Requirements[2]);
        Assert.Equal(new SemanticNameExpression("first"), first.Arguments[0]);
        Assert.Equal(new SemanticTextExpression("red"), first.Arguments[1]);
        Assert.Equal(new SemanticNameExpression("second"), second.Arguments[0]);
        Assert.Equal(new SemanticTextExpression("black"), second.Arguments[1]);
    }

    [Theory]
    [InlineData("Write a Python program using Pygame. Clear the missing to red.")]
    [InlineData("Write a Python program using Pygame. Open a window that is 32 wide and 24 tall. Clear the screen to.")]
    [InlineData("Write a Python program using Pygame. Clear to black.")]
    public void Does_not_generate_from_missing_receivers_or_colors(string request)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var error), error);
        if (!SequenceInputFactExtractor.TryExtract(knowledge!, request, out var facts, out error))
        {
            Assert.False(string.IsNullOrWhiteSpace(error));
            return;
        }
        Assert.True(new GenerationLanguagePackLoader().TryLoadEmbedded(out var packs, out error), error);
        Assert.False(SemanticProgramPlanner.TryGenerateProgram(facts, packs["python"], out var code, out _, out _));
        Assert.Empty(code);
    }

    [Fact]
    public void Plans_the_complete_pixel_probe_request()
    {
        const string request = "Write a Python program using Pygame. Initialize Pygame, open a window that is 32 wide and 24 tall saving result as screen. Clear the screen to black. Present the frame. Invoke api pygame-surface-pixel with arguments (screen, [0, 0]) saving result as pixel then print pixel.r + pixel.g + pixel.b. Quit Pygame.";
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var error), error);
        Assert.True(SequenceInputFactExtractor.TryExtract(knowledge!, request, out var facts, out error), error);
        Assert.True(new GenerationLanguagePackLoader().TryLoadEmbedded(out var packs, out error), error);
        Assert.True(SemanticProgramPlanner.TryGenerateProgram(facts, packs["python"], out _, out _, out error), error);
    }

    private static SemanticProgram Interpret(string request)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var error), error);
        Assert.True(SequenceInputFactExtractor.TryExtractProgram(knowledge!, request, out var program, out error), error);
        return Assert.IsType<SemanticProgram>(program);
    }
}
