using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class BreakRequirementTests
{
    [Fact]
    public void Requires_a_real_enclosing_repetition_not_just_indentation()
    {
        foreach (var statement in new ProgramRequirement[]
        {
            new BreakRequirement(),
            new ConditionalRequirement(new SemanticBooleanExpression(true), [new BreakRequirement()], [])
        })
        {
            var program = new SemanticProgram([statement]);
            Assert.False(SemanticProgramValidator.IsValid(program));
            Assert.False(SemanticProgramPlanner.TryPlan(program, Pack(), out var plan, out _));
            Assert.Empty(plan);
        }
    }

    [Fact]
    public void Does_not_inherit_loop_context_into_a_function_declaration()
    {
        var program = new SemanticProgram([new WhileRequirement(new SemanticBooleanExpression(true),
            [new FunctionRequirement("work", [new FunctionParameter("value", SemanticValueKind.Number)], null, [new BreakRequirement()])])]);
        Assert.False(SemanticProgramValidator.IsValid(program));
        Assert.False(SemanticProgramPlanner.TryPlan(program, Pack(), out _, out _));
    }

    [Theory]
    [InlineData("leave")]
    [InlineData("stop")]
    public void Projects_early_exit_at_its_ordered_position_inside_a_conditional(string verb)
    {
        var request = $"Write a Python program that set count to 0 then while count is less than 5 then set count to count + 1. When count is equal to 2 then {verb} repetition. End repetition. Print count.";
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var error), error);
        Assert.True(SequenceInputFactExtractor.TryExtractProgram(knowledge!, request, out var program, out error), error);
        var repetition = Assert.IsType<WhileRequirement>(program!.Requirements[1]);
        var conditional = Assert.IsType<ConditionalRequirement>(repetition.Body[1]);
        Assert.IsType<BreakRequirement>(Assert.Single(conditional.ThenRequirements));
        Assert.True(SequenceInputFactExtractor.TryExtract(knowledge!, request, out var facts, out error), error);
        Assert.True(SemanticProgramPlanner.TryGenerateProgram(facts, Pack(), out var code, out _, out error), error);
        Assert.Contains("        break", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_a_request_exit_after_a_closed_loop()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var error), error);
        Assert.False(SequenceInputFactExtractor.TryExtractProgram(knowledge!,
            "Write a Python program that set count to 0 then while count is less than 1 then set count to count + 1. End repetition. Leave repetition.",
            out _, out _));
    }

    private static GenerationLanguagePack Pack()
    {
        Assert.True(new GenerationLanguagePackLoader().TryLoadEmbedded(out var packs, out var error), error);
        return packs["python"];
    }
}
