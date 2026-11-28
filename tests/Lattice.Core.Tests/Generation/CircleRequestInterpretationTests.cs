using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class CircleRequestInterpretationTests
{
    [Theory]
    [InlineData("white", "20", "24", "8")]
    [InlineData("red", "10 + 2", "30", "2 * 3")]
    public void Captures_circle_arguments_and_plans_changed_values(string color, string x, string y, string radius)
    {
        var request = $"Write a Python program using Pygame. Open a window that is 64 wide and 48 tall. Draw the marker as a {color} circle at x {x} and y {y} with radius {radius}.";
        var (program, code) = Interpret(request);
        var call = Assert.IsType<CallRequirement>(program.Requirements[2]);
        Assert.Equal("pygame-draw-circle-dimensions", call.ApiContract);
        Assert.Equal(5, call.Arguments.Length);
        Assert.Equal(new SemanticTextExpression(color), call.Arguments[1]);
        Assert.Contains($"pygame.draw.circle(screen, \"{color}\", [{x}, {y}], {radius})", code, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Draw first as a white circle at x 20 and y 20 with radius 3 then draw second as a red rectangle at x 2 and y 3 with width 4 and height 5.", "circle", "rectangle")]
    [InlineData("Draw first as a white rectangle at x 2 and y 3 with width 4 and height 5. Draw second as a red circle at x 20 and y 20 with radius 3.", "rectangle", "circle")]
    public void Matches_each_shape_locally_instead_of_using_global_keywords(string drawing, string first, string second)
    {
        var (program, _) = Interpret("Write a Python program using Pygame. Open a window that is 64 wide and 48 tall. " + drawing);
        Assert.Equal(4, program.Requirements.Length);
        Assert.Equal($"pygame-draw-{first}-dimensions", Assert.IsType<CallRequirement>(program.Requirements[2]).ApiContract);
        Assert.Equal($"pygame-draw-{second}-dimensions", Assert.IsType<CallRequirement>(program.Requirements[3]).ApiContract);
    }

    [Theory]
    [InlineData("Draw marker as a white circle at x 20 and y 20 with radius missing.")]
    [InlineData("Draw marker as a white circle at x missing and y 20 with radius 3.")]
    public void Rejects_undefined_circle_dependencies(string drawing)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var error), error);
        Assert.True(SequenceInputFactExtractor.TryExtract(knowledge!, "Write a Python program using Pygame. Open a window that is 64 wide and 48 tall. " + drawing, out var facts, out error), error);
        Assert.True(new GenerationLanguagePackLoader().TryLoadEmbedded(out var packs, out error), error);
        Assert.False(SemanticProgramPlanner.TryGenerateProgram(facts, packs["python"], out _, out _, out _));
    }

    [Fact]
    public void Clause_lookahead_does_not_cross_sentence_then_or_quoted_tokens()
    {
        // Each would falsely activate the rectangle rule if its lookahead searched the entire request.
        foreach (var request in new[] { "Draw marker. Describe as rectangle.", "Draw marker then describe as rectangle.", "Draw marker as 'rectangle'." })
        {
            Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var error), error);
            Assert.True(SequenceInputFactExtractor.TryExtract(knowledge!, "Write a Python program using Pygame. " + request, out var facts, out error), error);
            Assert.DoesNotContain(facts, fact => fact.Id == "program.requirement.api-call");
        }
    }

    private static (SemanticProgram Program, string Code) Interpret(string request)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var error), error);
        Assert.True(SequenceInputFactExtractor.TryExtractProgram(knowledge!, request, out var program, out error), error);
        Assert.True(SequenceInputFactExtractor.TryExtract(knowledge!, request, out var facts, out error), error);
        Assert.True(new GenerationLanguagePackLoader().TryLoadEmbedded(out var packs, out error), error);
        Assert.True(SemanticProgramPlanner.TryGenerateProgram(facts, packs["python"], out var code, out _, out error), error);
        return (Assert.IsType<SemanticProgram>(program), code);
    }
}
