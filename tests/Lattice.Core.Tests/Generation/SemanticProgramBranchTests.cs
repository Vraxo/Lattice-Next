using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

/// <summary>
/// Multi-way branches: the chain requirement, its structural validation, and the planned block that
/// either continues the chain or closes it with an otherwise block.
/// </summary>
public sealed class SemanticProgramBranchTests
{
    [Fact]
    public void Validates_a_chain_whose_conditions_guard_their_own_statements()
    {
        Assert.True(SemanticProgramValidator.IsValid(CreateChainProgram()));
    }

    [Fact]
    public void Rejects_a_chain_without_a_condition()
    {
        var program = new SemanticProgram([
            new ConditionalChainRequirement(
                [],
                [new OutputRequirement(new SemanticTextExpression("low"))])
        ]);

        Assert.False(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Rejects_a_chain_whose_condition_guards_no_statement()
    {
        var program = new SemanticProgram([
            new ConditionalChainRequirement(
                [
                    new ConditionalBranch(
                        new SemanticBinaryExpression(
                            SemanticExpressionOperator.GreaterThanOrEqual,
                            new SemanticNumberExpression("20"),
                            new SemanticNumberExpression("10")),
                        [])
                ],
                [new OutputRequirement(new SemanticTextExpression("low"))])
        ]);

        Assert.False(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Plans_a_chain_that_falls_through_into_an_otherwise_block()
    {
        Assert.True(
            SemanticProgramPlanner.TryPlan(
                CreateChainProgram(),
                LoadPythonPack(),
                out var statements,
                out var error),
            error);

        Assert.Equal(
            "if 20 >= 10:\n    print(\"high\")\nelif 20 >= 5:\n    print(\"mid\")\nelse:\n    print(\"low\")",
            SemanticProgramPlanner.Render(statements));
    }

    [Fact]
    public void Declares_a_function_without_a_stated_result_kind()
    {
        var program = new SemanticProgram([
            new FunctionRequirement(
                "label",
                [new FunctionParameter("count", null)],
                null,
                [new ReturnRequirement(new SemanticNameExpression("count"))]),
            new OutputRequirement(new SemanticFunctionCallExpression(
                "label",
                [new SemanticNumberExpression("5")]))
        ]);

        Assert.True(SemanticProgramValidator.IsValid(program));
    }

    [Fact]
    public void Rejects_a_return_that_no_declaration_wraps()
    {
        var program = new SemanticProgram([
            new ReturnRequirement(new SemanticNumberExpression("5"))
        ]);

        Assert.False(SemanticProgramValidator.IsValid(program));
    }

    private static SemanticProgram CreateChainProgram() =>
        new([
            new ConditionalChainRequirement(
                [
                    new ConditionalBranch(
                        new SemanticBinaryExpression(
                            SemanticExpressionOperator.GreaterThanOrEqual,
                            new SemanticNumberExpression("20"),
                            new SemanticNumberExpression("10")),
                        [new OutputRequirement(new SemanticTextExpression("high"))]),
                    new ConditionalBranch(
                        new SemanticBinaryExpression(
                            SemanticExpressionOperator.GreaterThanOrEqual,
                            new SemanticNumberExpression("20"),
                            new SemanticNumberExpression("5")),
                        [new OutputRequirement(new SemanticTextExpression("mid"))])
                ],
                [new OutputRequirement(new SemanticTextExpression("low"))])
        ]);

    private static GenerationLanguagePack LoadPythonPack()
    {
        var loader = new GenerationLanguagePackLoader();
        Assert.True(loader.TryLoadEmbedded(out var packs, out var error), error);
        return packs["python"];
    }
}
