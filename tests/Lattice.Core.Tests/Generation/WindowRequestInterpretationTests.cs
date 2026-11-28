using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class WindowRequestInterpretationTests
{
    [Theory]
    [InlineData("640", "480", "screen", "")]
    [InlineData("240", "180", "canvas", " saving result as canvas")]
    public void Captures_window_dimensions_and_explicit_or_default_result_names(
        string width, string height, string name, string resultClause)
    {
        var program = Interpret($"Write a Python program using Pygame. Initialize Pygame, open a window that is {width} wide and {height} tall{resultClause}.");
        var binding = Assert.IsType<AssignmentRequirement>(program.Requirements[2]);
        Assert.Equal(name, binding.VariableName);
        var value = Assert.IsType<SemanticApiExpression>(binding.Value);
        Assert.Equal("pygame-window-dimensions", value.Contract);
        Assert.Equal(new SemanticNumberExpression(width), value.Arguments[0]);
        Assert.Equal(new SemanticNumberExpression(height), value.Arguments[1]);
    }

    [Fact]
    public void Associates_individual_arguments_with_the_correct_call()
    {
        var program = Interpret("Write a Python program using Pygame. Open a window that is 160 wide and 120 tall saving result as first then open a window that is 240 wide and 180 tall saving result as second.");
        var first = Assert.IsType<AssignmentRequirement>(program.Requirements[1]);
        var second = Assert.IsType<AssignmentRequirement>(program.Requirements[2]);
        Assert.Equal("first", first.VariableName);
        Assert.Equal("second", second.VariableName);
        Assert.Equal(new SemanticNumberExpression("160"), Assert.IsType<SemanticApiExpression>(first.Value).Arguments[0]);
        Assert.Equal(new SemanticNumberExpression("180"), Assert.IsType<SemanticApiExpression>(second.Value).Arguments[1]);
    }

    [Theory]
    [InlineData("Write a Python program using Pygame. Open a window that is 160 wide and tall.")]
    [InlineData("Write a Python program using Pygame. Open a window that is wide and 120 tall.")]
    [InlineData("Write a Python program using Pygame. Open a window that is missing wide and 120 tall.")]
    [InlineData("Write a Python program using Pygame. Open a window that is 160 wide and 120 tall with arguments ([320, 240]).")]
    public void Does_not_generate_from_missing_unavailable_or_conflicting_arguments(string request)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var error), error);
        if (!SequenceInputFactExtractor.TryExtract(knowledge!, request, out var facts, out error))
        {
            Assert.False(string.IsNullOrWhiteSpace(error));
            return;
        }

        Assert.True(new GenerationLanguagePackLoader().TryLoadEmbedded(out var packs, out error), error);
        Assert.False(SemanticProgramPlanner.TryGenerateProgram(facts, packs["python"], out var code, out var statements, out error));
        Assert.Empty(code);
        Assert.Empty(statements);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Does_not_guess_a_library_for_a_window_request()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var error), error);
        Assert.True(SequenceInputFactExtractor.TryExtract(knowledge!, "Write a Python program. Open a window that is 160 wide and 120 tall.", out var facts, out error), error);
        Assert.DoesNotContain(facts, fact => fact.Id == "program.requirement.api-call");
    }

    private static SemanticProgram Interpret(string request)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var error), error);
        Assert.True(SequenceInputFactExtractor.TryExtractProgram(knowledge!, request, out var program, out error), error);
        return Assert.IsType<SemanticProgram>(program);
    }
}
