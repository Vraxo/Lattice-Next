using System.Collections.Immutable;
using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class SemanticProgramTests
{
    [Theory]
    [InlineData("temperature", "21", "18", "warm", "cool")]
    [InlineData("balance", "43", "40", "sufficient", "insufficient")]
    public void Program_requirements_preserve_values_names_comparisons_branches_and_output(
        string variable,
        string assignedValue,
        string threshold,
        string output,
        string elseOutput)
    {
        var program = new SemanticProgram(
        [
            new AssignmentRequirement(variable, new SemanticNumberExpression(assignedValue)),
            new ConditionalRequirement(
                new SemanticBinaryExpression(
                    SemanticExpressionOperator.GreaterThanOrEqual,
                    new SemanticNameExpression(variable),
                    new SemanticNumberExpression(threshold)),
                [new OutputRequirement(new SemanticTextExpression(output))],
                [new OutputRequirement(new SemanticTextExpression(elseOutput))])
        ]);

        var assignment = Assert.IsType<AssignmentRequirement>(program.Requirements[0]);
        Assert.Equal(variable, assignment.VariableName);
        Assert.Equal(new SemanticNumberExpression(assignedValue), assignment.Value);

        var conditional = Assert.IsType<ConditionalRequirement>(program.Requirements[1]);
        var comparison = Assert.IsType<SemanticBinaryExpression>(conditional.Condition);
        Assert.Equal(SemanticExpressionOperator.GreaterThanOrEqual, comparison.Operator);
        Assert.Equal(new SemanticNameExpression(variable), comparison.Left);
        Assert.Equal(new SemanticNumberExpression(threshold), comparison.Right);
        Assert.Equal(new OutputRequirement(new SemanticTextExpression(output)),
            Assert.Single(conditional.ThenRequirements));
        Assert.Equal(new OutputRequirement(new SemanticTextExpression(elseOutput)),
            Assert.Single(conditional.ElseRequirements));
    }
}
