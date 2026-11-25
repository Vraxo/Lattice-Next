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
                                    [
                                        new ConditionalRequirement(
                                            new SemanticBooleanExpression(false),
                                            [new OutputRequirement(new SemanticTextExpression("deepest"))],
                                            [])
                                    ],
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
        Assert.Contains("at most 4 levels", error, StringComparison.Ordinal);
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

    [Fact]
    public void Realizes_iteration_over_a_bound_collection()
    {
        var statements = PlanWithCollections(
            ["scores"],
            new IterationRequirement(
                "score",
                new SemanticNameExpression("scores"),
                [new OutputRequirement(new SemanticNameExpression("score"))]));

        Assert.Equal(
            new[] { "python-bound-for-loop", "python-print-indented-expression" },
            statements.Select(statement => statement.OperationId));
        Assert.Equal(
            new[] { 0, 1 },
            statements.Select(statement => statement.Depth));
        Assert.Equal(
            string.Join('\n', "for score in scores:", "    print(score)"),
            SemanticProgramPlanner.Render(statements));
    }

    [Fact]
    public void Realizes_dictionary_iteration_inside_a_nested_block()
    {
        var statements = PlanWithCollections(
            ["entries"],
            new ConditionalRequirement(
                new SemanticBooleanExpression(true),
                [new DictionaryIterationRequirement(
                    "product",
                    "price",
                    new SemanticNameExpression("entries"),
                    [new OutputRequirement(new SemanticNameExpression("price"))])],
                []));

        Assert.Equal(
            new[] { "python-indented-bound-dictionary-loop", "python-print-double-indented-expression" },
            statements.Skip(1).Select(statement => statement.OperationId));
        Assert.Equal(
            string.Join(
                '\n',
                "if True:",
                "    for product, price in entries.items():",
                "        print(price)"),
            SemanticProgramPlanner.Render(statements));
    }

    [Fact]
    public void Realizes_an_accumulation_as_initialization_loop_and_update()
    {
        var statements = PlanWithCollections(
            ["scores"],
            new AccumulationRequirement(
                "total",
                SemanticCollectionItemKind.Number,
                "score",
                new SemanticNameExpression("scores"),
                new SemanticNumberExpression("0"),
                new SemanticBinaryExpression(
                    SemanticExpressionOperator.Add,
                    new SemanticNameExpression("total"),
                    new SemanticNameExpression("score"))));

        Assert.Equal(
            new[] { "python-bound-assignment", "python-bound-for-loop", "python-bound-indented-assignment" },
            statements.Select(statement => statement.OperationId));
        Assert.Equal(
            string.Join(
                '\n',
                "total = 0",
                "for score in scores:",
                "    total = total + score"),
            SemanticProgramPlanner.Render(statements));
    }

    [Fact]
    public void Realizes_a_filter_predicate_as_a_retained_element_binding()
    {
        var statements = PlanWithCollections(
            ["scores"],
            new FilterRequirement(
                "large",
                "score",
                new SemanticNameExpression("scores"),
                new SemanticBinaryExpression(
                    SemanticExpressionOperator.GreaterThanOrEqual,
                    new SemanticNameExpression("score"),
                    new SemanticNumberExpression("3"))));

        var statement = Assert.Single(statements);
        Assert.Equal("python-bound-filter", statement.OperationId);
        Assert.Equal("large = [score for score in scores if score >= 3]", statement.Text);
    }

    [Fact]
    public void Realizes_a_map_transformation_over_each_element()
    {
        var statements = PlanWithCollections(
            ["scores"],
            new MapRequirement(
                "doubled",
                "score",
                new SemanticNameExpression("scores"),
                new SemanticBinaryExpression(
                    SemanticExpressionOperator.Multiply,
                    new SemanticNameExpression("score"),
                    new SemanticNumberExpression("2"))));

        var statement = Assert.Single(statements);
        Assert.Equal("doubled = [score * 2 for score in scores]", statement.Text);
    }

    [Fact]
    public void Realizes_sorting_in_the_requested_direction()
    {
        var statements = PlanWithCollections(
            ["scores"],
            new SortRequirement("ordered", "score", new SemanticNameExpression("scores"), new SemanticNameExpression("score"), Descending: false),
            new SortRequirement("reversed", "score", new SemanticNameExpression("scores"), new SemanticNameExpression("score"), Descending: true));

        Assert.Equal(
            new[] { "python-bound-sort", "python-bound-reverse-sort" },
            statements.Select(statement => statement.OperationId));
        Assert.Equal(
            string.Join(
                '\n',
                "ordered = sorted(scores, key=lambda score: score)",
                "reversed = sorted(scores, key=lambda score: score, reverse=True)"),
            SemanticProgramPlanner.Render(statements));
    }

    [Fact]
    public void Realizes_projection_statements_inside_a_nested_block()
    {
        var statements = PlanWithCollections(
            ["scores"],
            new ConditionalRequirement(
                new SemanticBooleanExpression(true),
                [new MapRequirement(
                    "doubled",
                    "score",
                    new SemanticNameExpression("scores"),
                    new SemanticBinaryExpression(
                        SemanticExpressionOperator.Multiply,
                        new SemanticNameExpression("score"),
                        new SemanticNumberExpression("2")))],
                []));

        Assert.Equal(
            string.Join('\n', "if True:", "    doubled = [score * 2 for score in scores]"),
            SemanticProgramPlanner.Render(statements));
    }

    [Fact]
    public void Rejects_a_projection_whose_expression_uses_an_unknown_name()
    {
        var planned = SemanticProgramPlanner.TryPlan(
            new SemanticProgram([
                new MapRequirement(
                    "doubled",
                    "score",
                    new SemanticNameExpression("scores"),
                    new SemanticBinaryExpression(
                        SemanticExpressionOperator.Multiply,
                        new SemanticNameExpression("missing"),
                        new SemanticNumberExpression("2")))
            ])
            {
                Collections = [new CollectionBinding("scores", SemanticCollectionItemKind.Number)]
            },
            Pack,
            out var statements,
            out var error);

        Assert.False(planned);
        Assert.Empty(statements);
        Assert.Contains("missing", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_a_fourth_level_of_nested_blocks()
    {
        var planned = SemanticProgramPlanner.TryPlan(
            new SemanticProgram([
                new IterationRequirement(
                    "first",
                    new SemanticNameExpression("scores"),
                    [new IterationRequirement(
                        "second",
                        new SemanticNameExpression("scores"),
                        [new IterationRequirement(
                            "third",
                            new SemanticNameExpression("scores"),
                            [new OutputRequirement(new SemanticNameExpression("third"))])])])
            ])
            {
                Collections = [new CollectionBinding("scores", SemanticCollectionItemKind.Number)]
            },
            Pack,
            out var statements,
            out var error);

        Assert.False(planned);
        Assert.Empty(statements);
        Assert.Contains("at most 4 levels", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Realizes_a_function_declaration_with_parameters_a_return_and_a_call()
    {
        var statements = Plan(
            new FunctionRequirement(
                "add",
                [
                    new FunctionParameter("left", SemanticValueKind.Number),
                    new FunctionParameter("right", SemanticValueKind.Number)
                ],
                SemanticValueKind.Number,
                [new ReturnRequirement(new SemanticBinaryExpression(
                    SemanticExpressionOperator.Add,
                    new SemanticNameExpression("left"),
                    new SemanticNameExpression("right")))]),
            new CallRequirement(
                "add",
                [new SemanticNumberExpression("2"), new SemanticNumberExpression("3")]));

        Assert.Equal(
            new[]
            {
                "python-bound-function-header",
                "python-return-indented-expression",
                "python-bound-call"
            },
            statements.Select(statement => statement.OperationId));
        Assert.Equal(new[] { 0, 1, 0 }, statements.Select(statement => statement.Depth));
        Assert.Equal(
            string.Join('\n', "def add(left, right):", "    return left + right", "add(2, 3)"),
            SemanticProgramPlanner.Render(statements));
    }

    [Fact]
    public void Realizes_a_declaration_without_parameters_and_a_nullary_call()
    {
        var statements = Plan(
            new FunctionRequirement(
                "constant",
                [],
                SemanticValueKind.Number,
                [new ReturnRequirement(new SemanticNumberExpression("5"))]),
            new CallRequirement("constant", []));

        Assert.Equal(
            new[] { "python-bound-nullary-function-header", "python-return-indented-expression", "python-bound-nullary-call" },
            statements.Select(statement => statement.OperationId));
        Assert.Equal(
            string.Join('\n', "def constant():", "    return 5", "constant()"),
            SemanticProgramPlanner.Render(statements));
    }

    [Fact]
    public void Realizes_a_call_inside_a_nested_block()
    {
        var statements = Plan(new ConditionalRequirement(
            new SemanticBooleanExpression(true),
            [new CallRequirement("refresh", [new SemanticTextExpression("now")])],
            []));

        Assert.Equal(
            string.Join('\n', "if True:", "    refresh(\"now\")"),
            SemanticProgramPlanner.Render(statements));
    }

    [Fact]
    public void Rejects_a_return_at_the_top_level_of_a_program()
    {
        var planned = SemanticProgramPlanner.TryPlan(
            new SemanticProgram([new ReturnRequirement(new SemanticNumberExpression("5"))]),
            Pack,
            out var statements,
            out var error);

        Assert.False(planned);
        Assert.Empty(statements);
        Assert.Contains("inside a function", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_a_function_declaration_inside_a_block()
    {
        var planned = SemanticProgramPlanner.TryPlan(
            new SemanticProgram([
                new ConditionalRequirement(
                    new SemanticBooleanExpression(true),
                    [
                        new FunctionRequirement(
                            "constant",
                            [],
                            SemanticValueKind.Number,
                            [new ReturnRequirement(new SemanticNumberExpression("5"))])
                    ],
                    [])
            ]),
            Pack,
            out var statements,
            out var error);

        Assert.False(planned);
        Assert.Empty(statements);
        Assert.Contains("top level", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_a_call_argument_that_no_requirement_defines()
    {
        var planned = SemanticProgramPlanner.TryPlan(
            new SemanticProgram([new CallRequirement("add", [new SemanticNameExpression("missing")])]),
            Pack,
            out var statements,
            out var error);

        Assert.False(planned);
        Assert.Empty(statements);
        Assert.Contains("missing", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Realizes_a_class_with_initialized_fields_and_methods()
    {
        var statements = Plan(
            new ClassRequirement(
                "Counter",
                [new ClassField("count", new SemanticNumberExpression("0"))],
                [
                    new MethodRequirement(
                        "report",
                        [new FunctionParameter("amount", SemanticValueKind.Number)],
                        null,
                        [
                            new AssignmentRequirement(
                                "count",
                                new SemanticBinaryExpression(
                                    SemanticExpressionOperator.Add,
                                    new SemanticNameExpression("amount"),
                                    new SemanticNumberExpression("1"))),
                            new OutputRequirement(new SemanticMemberAccessExpression("self", "count"))
                        ])
                ]));

        Assert.Equal(
            string.Join(
                '\n',
                "class Counter:",
                "    def __init__(self):",
                "        self.count = 0",
                "    def report(self, amount):",
                "        count = amount + 1",
                "        print(self.count)"),
            SemanticProgramPlanner.Render(statements));
        Assert.Equal(
            new[]
            {
                "python-bound-class-header",
                "python-indented-bound-function-header",
                "python-bound-double-indented-field-assignment",
                "python-indented-bound-function-header",
                "python-bound-double-indented-assignment",
                "python-print-double-indented-expression"
            },
            statements.Select(statement => statement.OperationId));
    }

    [Fact]
    public void Realizes_a_class_without_fields_from_its_methods_only()
    {
        var statements = Plan(
            new ClassRequirement(
                "Greeter",
                [],
                [
                    new MethodRequirement(
                        "describe",
                        [],
                        SemanticValueKind.Text,
                        [new ReturnRequirement(new SemanticTextExpression("hello"))])
                ]));

        Assert.Equal(
            string.Join('\n', "class Greeter:", "    def describe(self):", "        return \"hello\""),
            SemanticProgramPlanner.Render(statements));
    }

    [Fact]
    public void Realizes_a_member_access_expression()
    {
        var statements = PlanWithCollections(
            ["accounts"],
            new IterationRequirement(
                "account",
                new SemanticNameExpression("accounts"),
                [new OutputRequirement(new SemanticMemberAccessExpression("account", "balance"))]));

        Assert.Equal(
            string.Join('\n', "for account in accounts:", "    print(account.balance)"),
            SemanticProgramPlanner.Render(statements));
    }

    [Fact]
    public void Realizes_a_method_call_on_a_bound_instance()
    {
        var statements = Plan(
            new AssignmentRequirement(
                "counter",
                new SemanticFunctionCallExpression("Counter", [])),
            new CallRequirement("report", [])
            {
                Receiver = "counter"
            });

        Assert.Equal(
            string.Join('\n', "counter = Counter()", "counter.report()"),
            SemanticProgramPlanner.Render(statements));
    }

    [Fact]
    public void Rejects_a_class_declaration_inside_a_block()
    {
        var planned = SemanticProgramPlanner.TryPlan(
            new SemanticProgram([
                new ConditionalRequirement(
                    new SemanticBooleanExpression(true),
                    [new ClassRequirement("Counter", [new ClassField("count", new SemanticNumberExpression("0"))], [])],
                    [])
            ]),
            Pack,
            out var statements,
            out var error);

        Assert.False(planned);
        Assert.Empty(statements);
        Assert.Contains("top level", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_a_method_call_on_an_instance_no_requirement_defines()
    {
        var planned = SemanticProgramPlanner.TryPlan(
            new SemanticProgram([new CallRequirement("report", []) { Receiver = "counter" }]),
            Pack,
            out var statements,
            out var error);

        Assert.False(planned);
        Assert.Empty(statements);
        Assert.Contains("counter", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Realizes_a_handled_failure_with_its_recovery_body()
    {
        var statements = Plan(
            new TryRequirement(
                [
                    new AssignmentRequirement(
                        "parsed",
                        new SemanticFunctionCallExpression("int", [new SemanticTextExpression("42")]))
                ],
                [
                    new HandlerRequirement(
                        "ValueError",
                        [new OutputRequirement(new SemanticTextExpression("not a number"))])
                ],
                "parsed"),
            new OutputRequirement(new SemanticNameExpression("parsed")));

        Assert.Equal(
            new[]
            {
                "python-bound-try-header",
                "python-bound-indented-assignment",
                "python-bound-handler-header",
                "python-print-indented-expression",
                "python-print-expression"
            },
            statements.Select(statement => statement.OperationId));
        Assert.Equal(
            string.Join(
                '\n',
                "try:",
                "    parsed = int(\"42\")",
                "except ValueError:",
                "    print(\"not a number\")",
                "print(parsed)"),
            SemanticProgramPlanner.Render(statements));
    }

    [Fact]
    public void Realizes_a_raise_guarded_by_a_condition()
    {
        var statements = Plan(
            new RaiseRequirement(
                "ValueError",
                new SemanticBinaryExpression(
                    SemanticExpressionOperator.GreaterThan,
                    new SemanticNumberExpression("5"),
                    new SemanticNumberExpression("3"))));

        Assert.Equal(
            string.Join('\n', "if 5 > 3:", "    raise ValueError"),
            SemanticProgramPlanner.Render(statements));
    }

    [Fact]
    public void Realizes_an_unguarded_raise()
    {
        var statements = Plan(new RaiseRequirement("RuntimeError", null));

        var statement = Assert.Single(statements);
        Assert.Equal("python-bound-raise", statement.OperationId);
        Assert.Equal("raise RuntimeError", statement.Text);
    }

    [Fact]
    public void Realizes_a_handled_failure_inside_a_function_body()
    {
        var statements = Plan(
            new FunctionRequirement(
                "parse_count",
                [new FunctionParameter("text", SemanticValueKind.Text)],
                SemanticValueKind.Number,
                [
                    new TryRequirement(
                        [new ReturnRequirement(new SemanticFunctionCallExpression(
                            "int",
                            [new SemanticNameExpression("text")]))],
                        [
                            new HandlerRequirement(
                                "ValueError",
                                [new ReturnRequirement(new SemanticNumberExpression("0"))])
                        ],
                        null)
                ]));

        Assert.Equal(
            string.Join(
                '\n',
                "def parse_count(text):",
                "    try:",
                "        return int(text)",
                "    except ValueError:",
                "        return 0"),
            SemanticProgramPlanner.Render(statements));
    }

    [Fact]
    public void Rejects_a_handled_result_the_attempt_never_binds()
    {
        var planned = SemanticProgramPlanner.TryPlan(
            new SemanticProgram([
                new TryRequirement(
                    [new OutputRequirement(new SemanticTextExpression("attempt"))],
                    [new HandlerRequirement("ValueError", [new OutputRequirement(new SemanticTextExpression("fail"))])],
                    "parsed")
            ]),
            Pack,
            out var statements,
            out var error);

        Assert.False(planned);
        Assert.Empty(statements);
        Assert.Contains("parsed", error, StringComparison.Ordinal);
    }

    private static ImmutableArray<PlannedProgramStatement> PlanWithCollections(
        ImmutableArray<string> collectionNames,
        params ProgramRequirement[] requirements)
    {
        var program = new SemanticProgram([.. requirements])
        {
            Collections = [.. collectionNames.Select(name =>
                new CollectionBinding(name, SemanticCollectionItemKind.Number))]
        };

        var planned = SemanticProgramPlanner.TryPlan(program, Pack, out var statements, out var error);
        Assert.True(planned, error);
        return statements;
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
