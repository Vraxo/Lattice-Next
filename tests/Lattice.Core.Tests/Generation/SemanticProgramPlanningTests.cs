using System.Collections.Immutable;
using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class SemanticProgramPlanningTests
{
    private static readonly GenerationLanguagePack Pack = LoadPack();

    [Fact]
    public void Realizes_a_scalar_assignment_through_the_assignment_operation()
    {
        var statement = Assert.Single(Plan(
            new AssignmentRequirement("total", new SemanticNumberExpression("5"))));

        Assert.Equal(0, statement.Depth);
        Assert.Equal("emit-assignment", statement.Effect);
        Assert.Equal("python-bound-assignment", statement.OperationId);
        Assert.Equal("total = 5", statement.Text);
    }

    [Fact]
    public void Realizes_arithmetic_from_operand_and_operator_semantics()
    {
        var statements = Plan(
            new AssignmentRequirement("total", new SemanticNumberExpression("5")),
            new AssignmentRequirement(
                "doubled",
                new SemanticBinaryExpression(
                    SemanticExpressionOperator.Multiply,
                    new SemanticNameExpression("total"),
                    new SemanticNumberExpression("2"))));

        Assert.Equal(
            new[] { "total = 5", "doubled = total * 2" },
            statements.Select(statement => statement.Text));
    }

    [Fact]
    public void Rejects_an_arithmetically_required_operator_the_pack_does_not_provide()
    {
        var syntax = Pack.InputSyntax!;
        var restricted = Pack with
        {
            InputSyntax = syntax with
            {
                ExpressionOperators = syntax.ExpressionOperators
                    .Where(operation => operation.Kind != "add")
                    .ToImmutableArray()
            }
        };

        var planned = SemanticProgramPlanner.TryPlan(
            new SemanticProgram([
                new AssignmentRequirement("total", new SemanticNumberExpression("5")),
                new AssignmentRequirement(
                    "sum",
                    new SemanticBinaryExpression(
                        SemanticExpressionOperator.Add,
                        new SemanticNameExpression("total"),
                        new SemanticNumberExpression("3")))
            ]),
            restricted,
            out var statements,
            out var error);

        Assert.False(planned);
        Assert.Empty(statements);
        Assert.Contains("cannot render", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Realizes_a_requested_comparison_condition_and_true_branch()
    {
        var statements = Plan(new ConditionalRequirement(
            new SemanticBinaryExpression(
                SemanticExpressionOperator.GreaterThanOrEqual,
                new SemanticNumberExpression("20"),
                new SemanticNumberExpression("18")),
            [new OutputRequirement(new SemanticTextExpression("adult"))],
            []));

        Assert.Equal(
            new[] { "if 20 >= 18:", "    print(\"adult\")" },
            statements.Select(statement => statement.Text));
        Assert.Equal(
            new[] { "python-bound-conditional", "python-print-indented-expression" },
            statements.Select(statement => statement.OperationId));
        Assert.Equal(new[] { 0, 1 }, statements.Select(statement => statement.Depth));
    }

    [Fact]
    public void Realizes_both_branches_of_a_requested_conditional()
    {
        var statements = Plan(
            new AssignmentRequirement("score", new SemanticNumberExpression("42")),
            new ConditionalRequirement(
                new SemanticBinaryExpression(
                    SemanticExpressionOperator.GreaterThanOrEqual,
                    new SemanticNameExpression("score"),
                    new SemanticNumberExpression("10")),
                [new OutputRequirement(new SemanticTextExpression("pass"))],
                [new OutputRequirement(new SemanticTextExpression("fail"))]));

        var text = SemanticProgramPlanner.Render(statements);
        Assert.Equal(
            string.Join(
                '\n',
                "score = 42",
                "if score >= 10:",
                "    print(\"pass\")",
                "else:",
                "    print(\"fail\")"),
            text);
    }

    [Fact]
    public void Preserves_bindings_from_preceding_assignments_into_branches()
    {
        var statements = Plan(
            new AssignmentRequirement("amount", new SemanticNumberExpression("12")),
            new ConditionalRequirement(
                new SemanticBinaryExpression(
                    SemanticExpressionOperator.GreaterThanOrEqual,
                    new SemanticNameExpression("amount"),
                    new SemanticNumberExpression("10")),
                [new OutputRequirement(new SemanticTextExpression("large"))],
                []));

        Assert.Equal(
            string.Join(
                '\n',
                "amount = 12",
                "if amount >= 10:",
                "    print(\"large\")"),
            SemanticProgramPlanner.Render(statements));
    }

    [Fact]
    public void Binds_a_computed_value_to_output_without_a_fixed_sample()
    {
        var statements = Plan(
            new AssignmentRequirement("total", new SemanticNumberExpression("5")),
            new AssignmentRequirement(
                "doubled",
                new SemanticBinaryExpression(
                    SemanticExpressionOperator.Multiply,
                    new SemanticNameExpression("total"),
                    new SemanticNumberExpression("2"))),
            new OutputRequirement(new SemanticNameExpression("doubled")));

        Assert.Equal("print(doubled)", statements[^1].Text);
        Assert.Equal("python-print-expression", statements[^1].OperationId);
    }

    [Fact]
    public void Binds_a_requested_literal_to_output()
    {
        var statement = Assert.Single(Plan(
            new OutputRequirement(new SemanticTextExpression("note: changed text"))));

        Assert.Equal("print(\"note: changed text\")", statement.Text);
    }

    [Fact]
    public void Rejects_a_value_used_before_it_is_assigned()
    {
        var planned = SemanticProgramPlanner.TryPlan(
            new SemanticProgram([new OutputRequirement(new SemanticNameExpression("total"))]),
            Pack,
            out var statements,
            out var error);

        Assert.False(planned);
        Assert.Empty(statements);
        Assert.Contains("before any requirement defines it", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_a_conditional_that_has_no_true_branch()
    {
        var planned = SemanticProgramPlanner.TryPlan(
            new SemanticProgram([
                new ConditionalRequirement(
                    new SemanticBooleanExpression(true),
                    [],
                    [new OutputRequirement(new SemanticTextExpression("fallback"))])
            ]),
            Pack,
            out _,
            out var error);

        Assert.False(planned);
        Assert.Contains("needs a true-branch statement", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_nesting_beyond_the_supported_depth()
    {
        var planned = SemanticProgramPlanner.TryPlan(
            new SemanticProgram([
                new ConditionalRequirement(
                    new SemanticBooleanExpression(true),
                    [
                        new ConditionalRequirement(
                            new SemanticBooleanExpression(false),
                            [new OutputRequirement(new SemanticTextExpression("outer"))],
                            []),
                        new ConditionalRequirement(
                            new SemanticBooleanExpression(false),
                            [
                                new ConditionalRequirement(
                                    new SemanticBooleanExpression(false),
                                    [new OutputRequirement(new SemanticTextExpression("inner"))],
                                    [])
                            ],
                            [])
                    ],
                    [])
            ]),
            Pack,
            out _,
            out var error);

        Assert.False(planned);
        Assert.Contains("at most 2 levels", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_an_assignment_target_the_pack_cannot_write()
    {
        var planned = SemanticProgramPlanner.TryPlan(
            new SemanticProgram([
                new AssignmentRequirement("not an identifier", new SemanticNumberExpression("5"))
            ]),
            Pack,
            out _,
            out var error);

        Assert.False(planned);
        Assert.Contains("is not an identifier", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_an_empty_semantic_program()
    {
        var planned = SemanticProgramPlanner.TryPlan(
            new SemanticProgram([]),
            Pack,
            out _,
            out var error);

        Assert.False(planned);
        Assert.Contains("between one and", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Plans_indented_statements_for_nested_blocks()
    {
        var statements = Plan(new ConditionalRequirement(
            new SemanticBooleanExpression(true),
            [
                new AssignmentRequirement("total", new SemanticNumberExpression("5")),
                new ConditionalRequirement(
                    new SemanticBinaryExpression(
                        SemanticExpressionOperator.Equal,
                        new SemanticNameExpression("total"),
                        new SemanticNumberExpression("5")),
                    [new OutputRequirement(new SemanticTextExpression("match"))],
                    [])
            ],
            []));

        Assert.Equal(
            string.Join(
                '\n',
                "if True:",
                "    total = 5",
                "    if total == 5:",
                "        print(\"match\")"),
            SemanticProgramPlanner.Render(statements));
    }

    private static ImmutableArray<PlannedProgramStatement> Plan(params ProgramRequirement[] requirements)
    {
        var planned = SemanticProgramPlanner.TryPlan(
            new SemanticProgram([.. requirements]),
            Pack,
            out var statements,
            out var error);
        Assert.True(planned, error);
        return statements;
    }

    private static GenerationLanguagePack LoadPack()
    {
        var loader = new GenerationLanguagePackLoader();
        Assert.True(loader.TryLoadEmbedded(out var packs, out var error), error);
        return packs["python"];
    }
}
