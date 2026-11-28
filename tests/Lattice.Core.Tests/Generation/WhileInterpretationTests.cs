using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class WhileInterpretationTests
{
    [Theory]
    [InlineData("count", "0", "3", "1")]
    [InlineData("remaining", "2", "9", "2")]
    public void Captures_the_condition_and_ordered_body_from_request_values(
        string name, string initial, string limit, string increment)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var error), error);
        var request = $"Write a Python program that set {name} to {initial} then while {name} is less than {limit} " +
            $"then print {name} then set {name} to {name} + {increment}.";
        Assert.True(SequenceInputFactExtractor.TryExtractProgram(knowledge!, request, out var program, out error), error);
        Assert.NotNull(program);
        Assert.Equal(new AssignmentRequirement(name, new SemanticNumberExpression(initial)), program.Requirements[0]);
        var loop = Assert.IsType<WhileRequirement>(program.Requirements[1]);
        Assert.Equal(new SemanticBinaryExpression(SemanticExpressionOperator.LessThan,
            new SemanticNameExpression(name), new SemanticNumberExpression(limit)), loop.Condition);
        Assert.Collection(loop.Body,
            statement => Assert.Equal(new OutputRequirement(new SemanticNameExpression(name)), statement),
            statement => Assert.Equal(new AssignmentRequirement(name, new SemanticBinaryExpression(
                SemanticExpressionOperator.Add, new SemanticNameExpression(name), new SemanticNumberExpression(increment))), statement));
    }

    [Theory]
    [InlineData("Write a Python program that set count to 0 then while count is less than 3 then.")]
    [InlineData("Write a Python program that set count to 0 then while count + * 3 then print count.")]
    [InlineData("Write a Python program that set count to 0 then while count is less than 3 then while count is less than 2 then print count.")]
    public void Rejects_incomplete_malformed_or_unscoped_bodies(string request)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var error), error);
        Assert.False(SequenceInputFactExtractor.TryExtractProgram(knowledge!, request, out var program, out error));
        Assert.Null(program);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}
