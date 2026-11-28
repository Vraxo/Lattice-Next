using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class NestedRepetitionInterpretationTests
{
    [Fact]
    public void Captures_while_iteration_and_condition_as_an_ordered_tree()
    {
        var program = Interpret("Write a Python program that set round to 0 then set text to 'ab' then while round is less than 2 then set round to round + 1. " +
            "For each letter in text then when letter is equal to 'b' then print letter. End repetition. Print round.");
        var loop = Assert.IsType<WhileRequirement>(program.Requirements[2]);
        Assert.Equal(3, loop.Body.Length);
        Assert.IsType<AssignmentRequirement>(loop.Body[0]);
        var iteration = Assert.IsType<IterationRequirement>(loop.Body[1]);
        Assert.Equal("letter", iteration.ItemName);
        Assert.Equal(new SemanticNameExpression("text"), iteration.Source);
        var branch = Assert.IsType<ConditionalRequirement>(Assert.Single(iteration.Body));
        Assert.Equal(new OutputRequirement(new SemanticNameExpression("letter")), Assert.Single(branch.ThenRequirements));
        Assert.Equal(new OutputRequirement(new SemanticNameExpression("round")), loop.Body[2]);
    }

    [Fact]
    public void Explicit_ends_keep_following_statements_outside_the_loop()
    {
        var program = Interpret("Write a Python program that set count to 0 then while count is less than 2 then set count to count + 1. " +
            "End the repetition. Print count.");
        Assert.Equal(3, program.Requirements.Length);
        Assert.IsType<WhileRequirement>(program.Requirements[1]);
        Assert.IsType<OutputRequirement>(program.Requirements[2]);
    }

    [Fact]
    public void Supports_two_closed_sibling_repetitions()
    {
        var program = Interpret("Write a Python program that set text to 'ab' then for each first in text then print first. End repetition. " +
            "For each second in text then print second. End repetition.");
        Assert.Equal(3, program.Requirements.Length);
        Assert.Equal("first", Assert.IsType<IterationRequirement>(program.Requirements[1]).ItemName);
        Assert.Equal("second", Assert.IsType<IterationRequirement>(program.Requirements[2]).ItemName);
    }

    [Theory]
    [InlineData("Write a Python program that set text to 'ab' then end repetition then print text.")]
    [InlineData("Write a Python program that set count to 0 then while count is less than 2 then end repetition then print count.")]
    [InlineData("Write a Python program that set count to 0 then while count is less than 2 then while count is less than 1 then print count.")]
    [InlineData("Write a Python program that set count to 0 then when count is equal to 0 then while count is less than 2 then print count. End repetition.")]
    [InlineData("Write a Python program that set text to 'ab' then for each letter in then print letter.")]
    public void Rejects_unmatched_empty_ambiguous_or_crossing_blocks(string request)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var error), error);
        Assert.False(SequenceInputFactExtractor.TryExtractProgram(knowledge!, request, out _, out error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    private static SemanticProgram Interpret(string request)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var error), error);
        Assert.True(SequenceInputFactExtractor.TryExtractProgram(knowledge!, request, out var program, out error), error);
        return Assert.IsType<SemanticProgram>(program);
    }
}
