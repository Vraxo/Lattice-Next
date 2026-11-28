using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class HeldInputInterpretationTests
{
    [Theory]
    [InlineData("W", "w", "position", 5)]
    [InlineData("S", "s", "offset", 3)]
    [InlineData("up arrow", "up", "height", 7)]
    [InlineData("down arrow", "down", "distance", 9)]
    public void Held_key_wording_is_a_conditional_action_not_a_blocking_repetition(string key, string contract, string variable, int step)
    {
        var (program, code) = Interpret($"Write a Python program using Pygame. Set {variable} to 10. While the {key} key is held, set {variable} to {variable} + {step}.");
        var branch = Assert.IsType<ConditionalRequirement>(program.Requirements[2]);
        var comparison = Assert.IsType<SemanticBinaryExpression>(branch.Condition);
        var index = Assert.IsType<SemanticSubscriptExpression>(comparison.Left);
        Assert.Equal("pygame-keys", Assert.IsType<SemanticApiExpression>(index.Target).Contract);
        Assert.Equal($"pygame-key-{contract}", Assert.IsType<SemanticApiExpression>(index.Key).Contract);
        Assert.IsType<AssignmentRequirement>(Assert.Single(branch.ThenRequirements));
        Assert.Contains($"{variable} = {variable} + {step}", code, StringComparison.Ordinal);
        Assert.DoesNotContain("while ", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Keeps_the_outer_repetition_and_orders_distinct_control_actions()
    {
        var (program, _) = Interpret("Write a Python program using Pygame. Set frame to 0 and set y to 10 then while frame is less than 2 then set frame to frame + 1. While the W key is held, set y to y - 5. While the S key is held, set y to y + 3. End repetition. Print y.");
        var outer = Assert.IsType<WhileRequirement>(program.Requirements[3]);
        Assert.Equal(3, outer.Body.Length);
        Assert.IsType<ConditionalRequirement>(outer.Body[1]);
        Assert.IsType<ConditionalRequirement>(outer.Body[2]);
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
