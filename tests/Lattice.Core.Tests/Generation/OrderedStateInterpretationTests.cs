using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class OrderedStateInterpretationTests
{
    [Fact]
    public void Sorts_mixed_assignment_verbs_by_source_position_not_rule_order()
    {
        var program = Interpret("Write a Python program. Assign first to 2. Set second to 5. Assigns first to second + 1 then prints first.");
        Assert.Collection(program.Requirements,
            statement => Assert.Equal(new AssignmentRequirement("first", new SemanticNumberExpression("2")), statement),
            statement => Assert.Equal(new AssignmentRequirement("second", new SemanticNumberExpression("5")), statement),
            statement => Assert.Equal(new AssignmentRequirement("first", new SemanticBinaryExpression(
                SemanticExpressionOperator.Add, new SemanticNameExpression("second"), new SemanticNumberExpression("1"))), statement),
            statement => Assert.Equal(new OutputRequirement(new SemanticNameExpression("first")), statement));
    }

    [Theory]
    [InlineData("Write a Python program. Set first to 2. Set second to then print first.")]
    [InlineData("Write a Python program. Set first to 2. Set second to first + * 3 then print first.")]
    public void Does_not_silently_drop_an_incomplete_or_malformed_later_assignment(string request)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var error), error);
        Assert.False(SequenceInputFactExtractor.TryExtractProgram(knowledge!, request, out var program, out error));
        Assert.Null(program);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Quoted_periods_do_not_split_values_or_lose_later_statements()
    {
        var program = Interpret("Write a Python program. Set label to 'first. second'. Set count to 2 then print label.");
        Assert.Equal(new AssignmentRequirement("label", new SemanticTextExpression("first. second")), program.Requirements[0]);
        Assert.Equal(3, program.Requirements.Length);
    }

    private static SemanticProgram Interpret(string request)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var error), error);
        Assert.True(SequenceInputFactExtractor.TryExtractProgram(knowledge!, request, out var program, out error), error);
        return Assert.IsType<SemanticProgram>(program);
    }
}
