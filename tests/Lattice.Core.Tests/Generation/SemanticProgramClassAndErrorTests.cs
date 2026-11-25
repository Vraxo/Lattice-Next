using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

/// <summary>
/// Class declarations, member access, raised errors, and handled failures, checked against the
/// structural validator without a language pack.
/// </summary>
public sealed class SemanticProgramClassAndErrorTests
{
    [Fact]
    public void Declares_a_class_with_fields_and_methods()
    {
        var program = new SemanticProgram([
            DeclareAccount(),
            new OutputRequirement(new SemanticFunctionCallExpression(
                "BankAccount",
                [new SemanticNumberExpression("10")]))
        ]);

        Assert.True(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Declares_a_class_with_only_methods()
    {
        var program = new SemanticProgram([
            new ClassRequirement(
                "Greeter",
                [],
                [
                    new MethodRequirement(
                        "describe",
                        [],
                        SemanticValueKind.Text,
                        [new ReturnRequirement(new SemanticTextExpression("hello"))])
                ])
        ]);

        Assert.True(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Rejects_a_class_without_fields_or_methods()
    {
        var program = new SemanticProgram([new ClassRequirement("Empty", [], [])]);

        Assert.False(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Rejects_two_fields_with_one_name()
    {
        var program = new SemanticProgram([
            new ClassRequirement(
                "Pair",
                [
                    new ClassField("value", new SemanticNumberExpression("1")),
                    new ClassField("value", new SemanticNumberExpression("2"))
                ],
                [])
        ]);

        Assert.False(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Rejects_a_method_returning_a_value_its_declared_result_cannot_hold()
    {
        var program = new SemanticProgram([
            new ClassRequirement(
                "Counter",
                [new ClassField("count", new SemanticNumberExpression("0"))],
                [
                    new MethodRequirement(
                        "count_text",
                        [],
                        SemanticValueKind.Number,
                        [new ReturnRequirement(new SemanticTextExpression("none"))])
                ])
        ]);

        Assert.False(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Accepts_a_return_inside_a_method_that_declares_no_result()
    {
        var program = new SemanticProgram([
            new ClassRequirement(
                "Counter",
                [new ClassField("count", new SemanticNumberExpression("0"))],
                [
                    new MethodRequirement(
                        "reset",
                        [],
                        null,
                        [new ReturnRequirement(new SemanticNumberExpression("0"))])
                ])
        ]);

        // A request that never states a result kind still returns a value, so an undeclared kind accepts it.
        Assert.True(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Accepts_a_method_that_returns_no_value()
    {
        var program = new SemanticProgram([
            new ClassRequirement(
                "Counter",
                [new ClassField("count", new SemanticNumberExpression("0"))],
                [
                    new MethodRequirement(
                        "reset",
                        [],
                        null,
                        [new AssignmentRequirement("count", new SemanticNumberExpression("0"))])
                ])
        ]);

        Assert.True(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Rejects_two_methods_with_one_name()
    {
        var program = new SemanticProgram([
            new ClassRequirement(
                "Counter",
                [new ClassField("count", new SemanticNumberExpression("0"))],
                [
                    new MethodRequirement("reset", [], null, [new OutputRequirement(new SemanticTextExpression("a"))]),
                    new MethodRequirement("reset", [], null, [new OutputRequirement(new SemanticTextExpression("b"))])
                ])
        ]);

        Assert.False(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Represents_member_access_over_its_owning_instance()
    {
        var program = new SemanticProgram([
            new OutputRequirement(new SemanticMemberAccessExpression("account", "balance"))
        ]);

        Assert.True(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Rejects_member_access_without_a_member()
    {
        var program = new SemanticProgram([
            new OutputRequirement(new SemanticMemberAccessExpression("account", ""))
        ]);

        Assert.False(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Treats_the_owning_instance_as_the_member_access_dependency()
    {
        var program = new SemanticProgram([
            new SortRequirement(
                "ordered",
                "record",
                new SemanticNameExpression("records"),
                new SemanticMemberAccessExpression("record", "name"),
                Descending: false)
        ])
        {
            Collections = [new CollectionBinding("records", SemanticCollectionItemKind.Text)]
        };

        Assert.True(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Rejects_a_sort_key_that_does_not_depend_on_the_item()
    {
        var program = new SemanticProgram([
            new SortRequirement(
                "ordered",
                "record",
                new SemanticNameExpression("records"),
                new SemanticMemberAccessExpression("other", "name"),
                Descending: false)
        ])
        {
            Collections = [new CollectionBinding("records", SemanticCollectionItemKind.Text)]
        };

        Assert.False(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Raises_an_error_when_a_condition_holds()
    {
        var program = new SemanticProgram([
            DeclareWithdrawal(),
            new RaiseRequirement(
                "ValueError",
                new SemanticBinaryExpression(
                    SemanticExpressionOperator.GreaterThan,
                    new SemanticNameExpression("amount"),
                    new SemanticNameExpression("balance")))
        ]);

        Assert.True(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Raises_an_error_without_a_condition()
    {
        var program = new SemanticProgram([new RaiseRequirement("ValueError", null)]);

        Assert.True(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Rejects_a_raise_whose_condition_is_not_boolean()
    {
        var program = new SemanticProgram([
            new RaiseRequirement("ValueError", new SemanticNumberExpression("5"))
        ]);

        Assert.False(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Handles_one_error_type_with_a_recovery_body()
    {
        var program = new SemanticProgram([
            new TryRequirement(
                [
                    new AssignmentRequirement(
                        "parsed",
                        new SemanticFunctionCallExpression("int", [new SemanticNameExpression("text")]))
                ],
                [
                    new HandlerRequirement(
                        "ValueError",
                        [new OutputRequirement(new SemanticTextExpression("not a number"))])
                ],
                "parsed")
        ]);

        Assert.True(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Rejects_a_try_without_a_handler()
    {
        var program = new SemanticProgram([
            new TryRequirement(
                [new OutputRequirement(new SemanticTextExpression("attempt"))],
                [],
                null)
        ]);

        Assert.False(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Rejects_a_try_without_a_body()
    {
        var program = new SemanticProgram([
            new TryRequirement(
                [],
                [new HandlerRequirement("ValueError", [new OutputRequirement(new SemanticTextExpression("fail"))])],
                null)
        ]);

        Assert.False(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Rejects_two_handlers_for_one_error_type()
    {
        var program = new SemanticProgram([
            new TryRequirement(
                [new OutputRequirement(new SemanticTextExpression("attempt"))],
                [
                    new HandlerRequirement("ValueError", [new OutputRequirement(new SemanticTextExpression("a"))]),
                    new HandlerRequirement("ValueError", [new OutputRequirement(new SemanticTextExpression("b"))])
                ],
                null)
        ]);

        Assert.False(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Rejects_a_handler_without_a_body()
    {
        var program = new SemanticProgram([
            new TryRequirement(
                [new OutputRequirement(new SemanticTextExpression("attempt"))],
                [new HandlerRequirement("ValueError", [])],
                null)
        ]);

        Assert.False(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Handles_a_failure_inside_a_function_body()
    {
        var program = new SemanticProgram([
            new FunctionRequirement(
                "parse_count",
                [new FunctionParameter("text", SemanticValueKind.Text)],
                SemanticValueKind.Number,
                [
                    new TryRequirement(
                        [new ReturnRequirement(new SemanticNumberExpression("0"))],
                        [
                            new HandlerRequirement(
                                "ValueError",
                                [new ReturnRequirement(new SemanticNumberExpression("0"))])
                        ],
                        null)
                ])
        ]);

        Assert.True(SemanticProgramValidator.IsValid(program));
    }

    private static ClassRequirement DeclareAccount() =>
        new(
            "BankAccount",
            [new ClassField("balance", new SemanticNumberExpression("0"))],
            [
                new MethodRequirement(
                    "deposit",
                    [new FunctionParameter("amount", SemanticValueKind.Number)],
                    null,
                    [new OutputRequirement(new SemanticNameExpression("amount"))]),
                new MethodRequirement(
                    "balance_value",
                    [],
                    SemanticValueKind.Number,
                    [new ReturnRequirement(new SemanticMemberAccessExpression("self", "balance"))])
            ]);

    private static FunctionRequirement DeclareWithdrawal() =>
        new(
            "withdraw",
            [
                new FunctionParameter("balance", SemanticValueKind.Number),
                new FunctionParameter("amount", SemanticValueKind.Number)
            ],
            SemanticValueKind.Number,
            [new ReturnRequirement(new SemanticNameExpression("balance"))]);
}
