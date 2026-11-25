using System.Collections.Immutable;
using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

/// <summary>
/// Function declarations, parameters, result kinds, returns, and calls, checked against the
/// structural validator without a language pack.
/// </summary>
public sealed class SemanticProgramFunctionTests
{
    [Fact]
    public void Declares_a_function_with_parameters_and_a_result()
    {
        var program = new SemanticProgram([
            new FunctionRequirement(
                "add",
                [
                    new FunctionParameter("left", SemanticValueKind.Number),
                    new FunctionParameter("right", SemanticValueKind.Number)
                ],
                SemanticValueKind.Number,
                [
                    new ReturnRequirement(new SemanticBinaryExpression(
                        SemanticExpressionOperator.Add,
                        new SemanticNameExpression("left"),
                        new SemanticNameExpression("right")))
                ])
        ]);

        Assert.True(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Declares_a_function_whose_body_uses_the_parameter_kinds()
    {
        var program = new SemanticProgram([
            new FunctionRequirement(
                "label",
                [new FunctionParameter("name", SemanticValueKind.Text)],
                SemanticValueKind.Text,
                [new ReturnRequirement(new SemanticNameExpression("name"))]),
            new OutputRequirement(new SemanticFunctionCallExpression(
                "label",
                [new SemanticTextExpression("Ada")]))
        ]);

        Assert.True(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Rejects_a_returned_value_whose_kind_differs_from_the_declared_result()
    {
        var program = new SemanticProgram([
            new FunctionRequirement(
                "label",
                [new FunctionParameter("count", SemanticValueKind.Number)],
                SemanticValueKind.Text,
                [new ReturnRequirement(new SemanticNumberExpression("5"))])
        ]);

        Assert.False(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Rejects_a_return_outside_a_function()
    {
        var program = new SemanticProgram([
            new ReturnRequirement(new SemanticNumberExpression("5"))
        ]);

        Assert.False(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Rejects_two_parameters_with_one_name()
    {
        var program = new SemanticProgram([
            new FunctionRequirement(
                "combine",
                [
                    new FunctionParameter("value", SemanticValueKind.Number),
                    new FunctionParameter("value", SemanticValueKind.Text)
                ],
                SemanticValueKind.Number,
                [new ReturnRequirement(new SemanticNameExpression("value"))])
        ]);

        Assert.False(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Rejects_a_declaration_without_a_body()
    {
        var program = new SemanticProgram([
            new FunctionRequirement(
                "empty",
                [new FunctionParameter("value", SemanticValueKind.Number)],
                SemanticValueKind.Number,
                [])
        ]);

        Assert.False(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Rejects_a_declaration_without_parameters()
    {
        var program = new SemanticProgram([
            new FunctionRequirement(
                "constant",
                [],
                SemanticValueKind.Number,
                [new ReturnRequirement(new SemanticNumberExpression("5"))])
        ]);

        Assert.False(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Rejects_a_declaration_with_more_parameters_than_the_model_allows()
    {
        var parameters = Enumerable.Range(0, SemanticProgramValidator.MaximumParameters + 1)
            .Select(index => new FunctionParameter($"value{index}", SemanticValueKind.Number))
            .ToImmutableArray();

        var program = new SemanticProgram([
            new FunctionRequirement(
                "many",
                parameters,
                SemanticValueKind.Number,
                [new ReturnRequirement(new SemanticNameExpression("value0"))])
        ]);

        Assert.False(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Rejects_two_declarations_with_one_name()
    {
        var first = new FunctionRequirement(
            "twice",
            [new FunctionParameter("value", SemanticValueKind.Number)],
            SemanticValueKind.Number,
            [new ReturnRequirement(new SemanticNameExpression("value"))]);
        var second = first with { ReturnKind = SemanticValueKind.Text };

        Assert.False(SemanticProgramValidator.IsValid(new SemanticProgram([first, second])));
    }

    [Fact]
    public void Binds_a_call_to_the_declared_parameters()
    {
        var program = new SemanticProgram([
            DeclareGreeting(),
            new CallRequirement("greet", [new SemanticTextExpression("Ada")])
        ]);

        Assert.True(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Binds_a_call_that_precedes_its_declaration()
    {
        var program = new SemanticProgram([
            new CallRequirement("greet", [new SemanticTextExpression("Ada")]),
            DeclareGreeting()
        ]);

        Assert.True(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Rejects_a_call_with_the_wrong_number_of_arguments()
    {
        var program = new SemanticProgram([
            DeclareGreeting(),
            new CallRequirement("greet", [])
        ]);

        Assert.False(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Rejects_a_call_whose_argument_kind_differs_from_the_parameter()
    {
        var program = new SemanticProgram([
            DeclareGreeting(),
            new CallRequirement("greet", [new SemanticNumberExpression("5")])
        ]);

        Assert.False(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Rejects_a_call_whose_argument_kind_is_known_only_through_another_declaration()
    {
        var program = new SemanticProgram([
            DeclareGreeting(),
            new FunctionRequirement(
                "count",
                [new FunctionParameter("value", SemanticValueKind.Number)],
                SemanticValueKind.Number,
                [new ReturnRequirement(new SemanticNameExpression("value"))]),
            new CallRequirement(
                "greet",
                [new SemanticFunctionCallExpression("count", [new SemanticNumberExpression("2")])])
        ]);

        Assert.False(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Accepts_an_argument_whose_kind_cannot_be_inferred()
    {
        var program = new SemanticProgram([
            DeclareGreeting(),
            new CallRequirement("greet", [new SemanticNameExpression("whoever")])
        ]);

        Assert.True(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Accepts_a_call_to_a_function_the_program_does_not_declare()
    {
        var program = new SemanticProgram([
            new CallRequirement("print", [new SemanticTextExpression("hello")])
        ]);

        Assert.True(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Rejects_a_function_requirement_with_an_undefined_result_kind()
    {
        var program = new SemanticProgram([
            new FunctionRequirement(
                "greet",
                [new FunctionParameter("name", SemanticValueKind.Text)],
                (SemanticValueKind)42,
                [new ReturnRequirement(new SemanticTextExpression("hello"))])
        ]);

        Assert.False(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Keeps_the_existing_collection_requirements_valid()
    {
        var program = new SemanticProgram([
            new FilterRequirement(
                "large",
                "value",
                new SemanticNameExpression("values"),
                new SemanticBinaryExpression(
                    SemanticExpressionOperator.GreaterThan,
                    new SemanticNameExpression("value"),
                    new SemanticNumberExpression("2")))
        ])
        {
            Collections = [new CollectionBinding("values", SemanticCollectionItemKind.Number)]
        };

        Assert.True(SemanticProgramValidator.IsValid(program));
    }

    private static FunctionRequirement DeclareGreeting() =>
        new(
            "greet",
            [new FunctionParameter("name", SemanticValueKind.Text)],
            SemanticValueKind.Text,
            [new ReturnRequirement(new SemanticNameExpression("name"))]);
}
