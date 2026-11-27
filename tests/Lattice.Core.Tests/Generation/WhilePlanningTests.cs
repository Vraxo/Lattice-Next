using System.Collections.Immutable;
using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class WhilePlanningTests
{
    private static readonly GenerationLanguagePack Pack = LoadPack();
    private static SemanticExpression Condition => new SemanticBinaryExpression(
        SemanticExpressionOperator.LessThan, new SemanticNameExpression("count"), new SemanticNumberExpression("3"));
    private static ProgramRequirement Initial => new AssignmentRequirement("count", new SemanticNumberExpression("0"));
    private static ProgramRequirement Update => new AssignmentRequirement("count", new SemanticBinaryExpression(
        SemanticExpressionOperator.Add, new SemanticNameExpression("count"), new SemanticNumberExpression("1")));

    [Fact]
    public void Realizes_a_loop_and_keeps_existing_bindings_available_after_it()
    {
        var statements = Plan(Initial, new WhileRequirement(Condition, [Update]),
            new OutputRequirement(new SemanticNameExpression("count")));
        Assert.Equal(new[] { "count = 0", "while count < 3:", "    count = count + 1", "print(count)" },
            statements.Select(statement => statement.Text));
        Assert.Equal("python-bound-while", statements[1].OperationId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Realizes_headers_at_supported_depths(int depth)
    {
        ProgramRequirement loop = new WhileRequirement(Condition, [new ContinueRequirement()]);
        for (var index = 0; index < depth; index++)
        {
            loop = new WhileRequirement(Condition, [loop]);
        }
        var statements = Plan(Initial, loop);
        Assert.Equal(new string(' ', depth * 4) + "while count < 3:", statements[depth + 1].Text);
        Assert.Equal(depth, statements[depth + 1].Depth);
    }

    [Fact]
    public void Rejects_unbound_condition_and_update_values_without_a_partial_plan()
    {
        AssertRejected([new WhileRequirement(Condition, [Update])], "count");
        AssertRejected([Initial, new WhileRequirement(Condition,
            [new AssignmentRequirement("count", new SemanticNameExpression("missing"))])], "missing");
    }

    [Fact]
    public void Does_not_export_a_binding_from_a_possibly_empty_loop()
    {
        AssertRejected([Initial, new WhileRequirement(Condition,
            [new AssignmentRequirement("temporary", new SemanticNumberExpression("1"))]),
            new OutputRequirement(new SemanticNameExpression("temporary"))], "temporary");
    }

    [Fact]
    public void Rejects_a_missing_pack_operation()
    {
        var restricted = Pack with
        {
            Operations = Pack.Operations.Where(operation => operation.Effect != "emit-while-header").ToImmutableArray()
        };
        Assert.False(SemanticProgramPlanner.TryPlan(new SemanticProgram([
            Initial, new WhileRequirement(Condition, [Update])]), restricted, out var statements, out _));
        Assert.Empty(statements);
    }

    [Fact]
    public void Rejects_empty_bodies_non_boolean_conditions_and_unsupported_depth()
    {
        AssertRejected([Initial, new WhileRequirement(Condition, [])], "body");
        AssertRejected([Initial, new WhileRequirement(new SemanticNumberExpression("1"), [Update])], "boolean");
        ProgramRequirement loop = new ContinueRequirement();
        for (var depth = 0; depth < 4; depth++)
        {
            loop = new WhileRequirement(Condition, [loop]);
        }
        AssertRejected([Initial, loop], "nesting");
    }

    private static ImmutableArray<PlannedProgramStatement> Plan(params ProgramRequirement[] requirements)
    {
        Assert.True(SemanticProgramPlanner.TryPlan(new SemanticProgram([.. requirements]), Pack,
            out var statements, out var error), error);
        return statements;
    }

    private static void AssertRejected(ImmutableArray<ProgramRequirement> requirements, string reason)
    {
        Assert.False(SemanticProgramPlanner.TryPlan(new SemanticProgram(requirements), Pack,
            out var statements, out var error));
        Assert.Empty(statements);
        Assert.Contains(reason, error, StringComparison.Ordinal);
    }

    private static GenerationLanguagePack LoadPack()
    {
        Assert.True(new GenerationLanguagePackLoader().TryLoadEmbedded(out var packs, out var error), error);
        return packs["python"];
    }
}
