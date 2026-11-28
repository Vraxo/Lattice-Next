using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class TimingRequestInterpretationTests
{
    [Theory]
    [InlineData("Limit", "60", "clock")]
    [InlineData("Cap", "30", "timer")]
    [InlineData("Limit", "20 + 5", "frameClock")]
    public void Plans_changed_rates_receivers_and_explicit_elapsed_result(string verb, string rate, string name)
    {
        var request = $"Write a Python program using Pygame. Create a clock saving result as {name}. " +
            $"{verb} frames to {rate} frames per second using the {name} saving result as elapsed then print elapsed / 1000.";
        var program = Interpret(request);
        var construction = Assert.IsType<AssignmentRequirement>(program.Requirements[1]);
        Assert.Equal("pygame-clock", Assert.IsType<SemanticApiExpression>(construction.Value).Contract);
        Assert.Equal(name, construction.VariableName);
        var binding = Assert.IsType<AssignmentRequirement>(program.Requirements[2]);
        var tick = Assert.IsType<SemanticApiExpression>(binding.Value);
        Assert.Equal("pygame-tick", tick.Contract);
        Assert.Equal(new SemanticNameExpression(name), tick.Arguments[0]);
        Assert.Equal("elapsed", binding.VariableName);
        var code = Generate(request);
        Assert.Contains($"{name} = pygame.time.Clock()", code, StringComparison.Ordinal);
        Assert.Contains($"elapsed = ({name}).tick({rate})", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Uses_default_clock_binding_and_retains_multiple_call_arguments()
    {
        var code = Generate("Write a Python program using Pygame. Create a clock. Limit frames to 60 frames per second using clock then cap frames to 30 frames per second using clock.");
        Assert.Contains("clock = pygame.time.Clock()", code, StringComparison.Ordinal);
        Assert.Contains("(clock).tick(60)\n(clock).tick(30)", code, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Write a Python program using Pygame. Limit frames to 60 frames per second using missing.")]
    [InlineData("Write a Python program using Pygame. Create a clock. Limit frames to missing frames per second using clock.")]
    public void Rejects_unavailable_receiver_or_rate(string request)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var error), error);
        Assert.True(SequenceInputFactExtractor.TryExtract(knowledge!, request, out var facts, out error), error);
        Assert.True(new GenerationLanguagePackLoader().TryLoadEmbedded(out var packs, out error), error);
        Assert.False(SemanticProgramPlanner.TryGenerateProgram(facts, packs["python"], out var code, out _, out _));
        Assert.Empty(code);
    }

    private static SemanticProgram Interpret(string request)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var error), error);
        Assert.True(SequenceInputFactExtractor.TryExtractProgram(knowledge!, request, out var program, out error), error);
        return Assert.IsType<SemanticProgram>(program);
    }

    private static string Generate(string request)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var error), error);
        Assert.True(SequenceInputFactExtractor.TryExtract(knowledge!, request, out var facts, out error), error);
        Assert.True(new GenerationLanguagePackLoader().TryLoadEmbedded(out var packs, out error), error);
        Assert.True(SemanticProgramPlanner.TryGenerateProgram(facts, packs["python"], out var code, out _, out error), error);
        return code;
    }
}
