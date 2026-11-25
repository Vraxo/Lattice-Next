using System.Collections.Immutable;
using System.Diagnostics;
using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Runtime.Tests;

/// <summary>
/// Plans function, class, and error requirements onto the Python language pack and executes the
/// generated program, so declarations, calls, returns, member access, and failure paths are verified
/// by behavior rather than by comparison with a fixed template.
/// </summary>
public sealed class GeneratedFunctionProgramExecutionTests
{
    private static readonly GenerationLanguagePack Pack = LoadPack();

    [Theory]
    [InlineData("add", "left", "right", "2", "3", "5")]
    [InlineData("add", "left", "right", "-4", "6", "2")]
    [InlineData("combine", "first", "second", "10", "15", "25")]
    public async Task Calls_a_generated_function_with_varied_arguments(
        string functionName,
        string firstParameterName,
        string secondParameterName,
        string firstArgument,
        string secondArgument,
        string expectedOutput)
    {
        var code = PlanAndRender(
            new FunctionRequirement(
                functionName,
                [
                    new FunctionParameter(firstParameterName, SemanticValueKind.Number),
                    new FunctionParameter(secondParameterName, SemanticValueKind.Number)
                ],
                SemanticValueKind.Number,
                [new ReturnRequirement(new SemanticBinaryExpression(
                    SemanticExpressionOperator.Add,
                    new SemanticNameExpression(firstParameterName),
                    new SemanticNameExpression(secondParameterName)))]),
            new OutputRequirement(new SemanticFunctionCallExpression(
                functionName,
                [new SemanticNumberExpression(firstArgument), new SemanticNumberExpression(secondArgument)])));

        await AssertProgramOutputAsync(code, expectedOutput);
    }

    [Theory]
    [InlineData("10", "4", "6")]
    [InlineData("4", "10", "-6")]
    public async Task Binds_arguments_positionally_to_the_declared_parameters(
        string firstArgument,
        string secondArgument,
        string expectedOutput)
    {
        var code = PlanAndRender(
            new FunctionRequirement(
                "subtract",
                [
                    new FunctionParameter("left", SemanticValueKind.Number),
                    new FunctionParameter("right", SemanticValueKind.Number)
                ],
                SemanticValueKind.Number,
                [new ReturnRequirement(new SemanticBinaryExpression(
                    SemanticExpressionOperator.Subtract,
                    new SemanticNameExpression("left"),
                    new SemanticNameExpression("right")))]),
            new OutputRequirement(new SemanticFunctionCallExpression(
                "subtract",
                [new SemanticNumberExpression(firstArgument), new SemanticNumberExpression(secondArgument)])));

        await AssertProgramOutputAsync(code, expectedOutput);
    }

    [Fact]
    public async Task Returns_and_prints_a_text_parameter()
    {
        var code = PlanAndRender(
            new FunctionRequirement(
                "label",
                [new FunctionParameter("name", SemanticValueKind.Text)],
                SemanticValueKind.Text,
                [new ReturnRequirement(new SemanticNameExpression("name"))]),
            new OutputRequirement(new SemanticFunctionCallExpression(
                "label",
                [new SemanticTextExpression("Ada")])));

        await AssertProgramOutputAsync(code, "Ada");
    }

    [Fact]
    public async Task Verifies_initialization_method_calls_and_member_values()
    {
        var code = PlanAndRender(
            new ClassRequirement(
                "Counter",
                [new ClassField("count", new SemanticNumberExpression("0"))],
                [
                    new MethodRequirement(
                        "report",
                        [],
                        null,
                        [new OutputRequirement(new SemanticMemberAccessExpression("self", "count"))])
                ]),
            new AssignmentRequirement(
                "counter",
                new SemanticFunctionCallExpression("Counter", [])),
            new CallRequirement("report", []) { Receiver = "counter" },
            new OutputRequirement(new SemanticMemberAccessExpression("counter", "count")));

        await AssertProgramOutputAsync(code, "0\n0");
    }

    [Fact]
    public async Task Verifies_a_handled_failure_on_both_paths()
    {
        var code = PlanAndRender(
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
                ]),
            new OutputRequirement(new SemanticFunctionCallExpression(
                "parse_count",
                [new SemanticTextExpression("42")])),
            new OutputRequirement(new SemanticFunctionCallExpression(
                "parse_count",
                [new SemanticTextExpression("abc")])));

        await AssertProgramOutputAsync(code, "42\n0");
    }

    [Fact]
    public async Task Raises_an_unhandled_error()
    {
        var code = PlanAndRender(new RaiseRequirement("ValueError", null));

        await AssertProgramFailsAsync(code, "ValueError");
    }

    [Fact]
    public async Task Raises_only_when_the_condition_holds()
    {
        var code = PlanAndRender(
            new FunctionRequirement(
                "check",
                [new FunctionParameter("amount", SemanticValueKind.Number)],
                SemanticValueKind.Number,
                [
                    new ConditionalRequirement(
                        new SemanticBinaryExpression(
                            SemanticExpressionOperator.GreaterThan,
                            new SemanticNameExpression("amount"),
                            new SemanticNumberExpression("10")),
                        [new RaiseRequirement("ValueError", null)],
                        []),
                    new ReturnRequirement(new SemanticNameExpression("amount"))
                ]),
            new OutputRequirement(new SemanticFunctionCallExpression(
                "check",
                [new SemanticNumberExpression("5")])),
            new OutputRequirement(new SemanticFunctionCallExpression(
                "check",
                [new SemanticNumberExpression("20")])));

        await AssertProgramFailsAsync(code, "ValueError", expectedPartialOutput: "5");
    }

    private static string PlanAndRender(params ProgramRequirement[] requirements)
    {
        var program = new SemanticProgram([.. requirements]);
        Assert.True(SemanticProgramPlanner.TryPlan(program, Pack, out var statements, out var error), error);
        return SemanticProgramPlanner.Render(statements);
    }

    private static async Task AssertProgramOutputAsync(string code, string expectedOutput)
    {
        var (exitCode, output, error) = await RunAsync(code);

        Assert.True(exitCode == 0, error + Environment.NewLine + code);
        Assert.Equal(expectedOutput + "\n", output);
    }

    private static async Task AssertProgramFailsAsync(
        string code,
        string expectedErrorFragment,
        string? expectedPartialOutput = null)
    {
        var (exitCode, output, error) = await RunAsync(code);

        Assert.True(exitCode != 0, output + Environment.NewLine + code);
        Assert.Contains(expectedErrorFragment, error, StringComparison.Ordinal);
        if (expectedPartialOutput is not null)
        {
            Assert.Equal(expectedPartialOutput + "\n", output);
        }
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunAsync(string code)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "python" : "python3",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(code);

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        var outputTask = process!.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var output = (await outputTask).Replace("\r\n", "\n", StringComparison.Ordinal);
        var error = await errorTask;
        return (process.ExitCode, output, error);
    }

    private static GenerationLanguagePack LoadPack()
    {
        var loader = new GenerationLanguagePackLoader();
        Assert.True(loader.TryLoadEmbedded(out var packs, out var error), error);
        return packs["python"];
    }
}
