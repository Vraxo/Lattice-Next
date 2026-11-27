using System.Collections.Immutable;
using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class WhileRequirementTests
{
    private static SemanticExpression Condition => new SemanticBinaryExpression(
        SemanticExpressionOperator.LessThan,
        new SemanticNameExpression("count"),
        new SemanticNumberExpression("3"));

    [Fact]
    public void Preserves_condition_and_ordered_body()
    {
        ProgramRequirement first = new OutputRequirement(new SemanticNameExpression("count"));
        ProgramRequirement second = new AssignmentRequirement("count", new SemanticBinaryExpression(
            SemanticExpressionOperator.Add,
            new SemanticNameExpression("count"),
            new SemanticNumberExpression("1")));
        var loop = new WhileRequirement(Condition, [first, second]);
        var program = new SemanticProgram([
            new AssignmentRequirement("count", new SemanticNumberExpression("0")), loop]);

        Assert.True(SemanticProgramValidator.IsValid(program));
        Assert.Equal(Condition, loop.Condition);
        Assert.Equal(first, loop.Body[0]);
        Assert.Equal(second, loop.Body[1]);
    }

    [Fact]
    public void Rejects_missing_bodies_and_malformed_or_non_boolean_conditions()
    {
        Assert.False(IsValid(new WhileRequirement(Condition, default)));
        Assert.False(IsValid(new WhileRequirement(Condition, [])));
        foreach (var condition in new SemanticExpression[]
        {
            new SemanticNameExpression("not a name"),
            new SemanticNumberExpression("1"),
            new SemanticTextExpression("yes")
        })
        {
            Assert.False(IsValid(new WhileRequirement(condition, [new ContinueRequirement()])));
        }
        Assert.False(IsValid(new WhileRequirement(Condition,
            [new AssignmentRequirement("not a name", new SemanticNumberExpression("0"))])));
    }

    [Fact]
    public void Accepts_boolean_literals_and_preserves_loop_context_through_a_branch()
    {
        Assert.True(IsValid(new WhileRequirement(new SemanticBooleanExpression(true),
            [new ConditionalRequirement(Condition, [new ContinueRequirement()], [])])));
        Assert.False(SemanticProgramValidator.IsValid(new SemanticProgram([
            new WhileRequirement(Condition, [new ContinueRequirement()]),
            new ContinueRequirement()])));
    }

    [Fact]
    public void Enforces_shared_nesting_and_body_size_limits()
    {
        ProgramRequirement statement = new OutputRequirement(new SemanticTextExpression("tick"));
        for (var depth = 0; depth < SemanticProgramValidator.MaximumDepth; depth++)
        {
            statement = new WhileRequirement(Condition, [statement]);
        }
        Assert.True(IsValid(statement));
        Assert.False(IsValid(new WhileRequirement(Condition, [statement])));
        Assert.False(IsValid(new WhileRequirement(Condition,
            Enumerable.Repeat<ProgramRequirement>(new ContinueRequirement(),
                SemanticProgramValidator.MaximumRequirements + 1).ToImmutableArray())));
    }

    [Theory]
    [InlineData(SemanticValueKind.Boolean, true)]
    [InlineData(SemanticValueKind.Number, false)]
    [InlineData(SemanticValueKind.Text, false)]
    public void Checks_the_declared_kind_of_a_condition_parameter(SemanticValueKind kind, bool expected)
    {
        var program = new SemanticProgram([new FunctionRequirement(
            "repeat", [new FunctionParameter("active", kind)], null,
            [new WhileRequirement(new SemanticNameExpression("active"), [new ContinueRequirement()])])]);
        Assert.Equal(expected, SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Does_not_allow_continue_to_cross_a_function_boundary()
    {
        Assert.False(IsValid(new WhileRequirement(Condition, [new FunctionRequirement(
            "report", [new FunctionParameter("value", SemanticValueKind.Number)], null,
            [new ContinueRequirement()])])));
    }

    private static bool IsValid(ProgramRequirement requirement) =>
        SemanticProgramValidator.IsValid(new SemanticProgram([requirement]));
}
