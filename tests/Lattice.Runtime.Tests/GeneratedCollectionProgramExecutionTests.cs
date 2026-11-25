using System.Collections.Immutable;
using System.Diagnostics;
using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Runtime.Tests;

/// <summary>
/// Plans collection and iteration requirements onto the Python language pack and executes the
/// generated program, so the generic composition is verified by its behavior rather than by
/// comparison with a fixed fragment. The request supplies the data line; every statement after it
/// is planned from the requirements.
/// </summary>
public sealed class GeneratedCollectionProgramExecutionTests
{
    private static readonly GenerationLanguagePack Pack = LoadPack();

    [Fact]
    public async Task Iterates_a_collection_in_source_order()
    {
        var code = PlanAndRender(
            ["scores"],
            new IterationRequirement(
                "score",
                new SemanticNameExpression("scores"),
                [new OutputRequirement(new SemanticNameExpression("score"))]));

        await AssertProgramOutputAsync("scores = [4, 10, 7]", code, "4\n10\n7");
    }

    [Theory]
    [InlineData("values = [1, 2, 3, 4]", "values", "total", "item", "10")]
    [InlineData("weights = [2.5, 1.5]", "weights", "sum_of_weights", "weight", "4.0")]
    [InlineData("counts = [7]", "counts", "running", "count", "7")]
    public async Task Accumulates_collections_of_varied_size_and_values(
        string data,
        string collection,
        string accumulator,
        string item,
        string expectedOutput)
    {
        var code = PlanAndRender(
            [collection],
            new AccumulationRequirement(
                accumulator,
                SemanticCollectionItemKind.Number,
                item,
                new SemanticNameExpression(collection),
                new SemanticNumberExpression("0"),
                new SemanticBinaryExpression(
                    SemanticExpressionOperator.Add,
                    new SemanticNameExpression(accumulator),
                    new SemanticNameExpression(item))),
            new OutputRequirement(new SemanticNameExpression(accumulator)));

        await AssertProgramOutputAsync(data, code, expectedOutput);
    }

    [Theory]
    [InlineData("values = [1, 2, 3, 4, 5]", "values", "value", "kept", SemanticExpressionOperator.GreaterThan, "2", "[3, 4, 5]")]
    [InlineData("readings = [1, 2, 3, 4, 5]", "readings", "reading", "large_readings", SemanticExpressionOperator.GreaterThanOrEqual, "4", "[4, 5]")]
    [InlineData("samples = [1, 2, 3, 4, 5]", "samples", "sample", "small_samples", SemanticExpressionOperator.LessThan, "3", "[1, 2]")]
    public async Task Filters_with_varied_predicates_and_names(
        string data,
        string collection,
        string item,
        string result,
        SemanticExpressionOperator comparison,
        string threshold,
        string expectedOutput)
    {
        var code = PlanAndRender(
            [collection],
            new FilterRequirement(
                result,
                item,
                new SemanticNameExpression(collection),
                new SemanticBinaryExpression(
                    comparison,
                    new SemanticNameExpression(item),
                    new SemanticNumberExpression(threshold))),
            new OutputRequirement(new SemanticNameExpression(result)));

        await AssertProgramOutputAsync(data, code, expectedOutput);
    }

    [Theory]
    [InlineData("numbers = [1, 2, 3]", "numbers", "number", "doubled", SemanticExpressionOperator.Multiply, "2", "[2, 4, 6]")]
    [InlineData("values = [1, 2, 3]", "values", "value", "shifted", SemanticExpressionOperator.Add, "10", "[11, 12, 13]")]
    [InlineData("counts = [10, 20]", "counts", "count", "halved", SemanticExpressionOperator.Divide, "2", "[5.0, 10.0]")]
    public async Task Maps_with_varied_transformations_and_names(
        string data,
        string collection,
        string item,
        string result,
        SemanticExpressionOperator transformation,
        string operand,
        string expectedOutput)
    {
        var code = PlanAndRender(
            [collection],
            new MapRequirement(
                result,
                item,
                new SemanticNameExpression(collection),
                new SemanticBinaryExpression(
                    transformation,
                    new SemanticNameExpression(item),
                    new SemanticNumberExpression(operand))),
            new OutputRequirement(new SemanticNameExpression(result)));

        await AssertProgramOutputAsync(data, code, expectedOutput);
    }

    [Theory]
    [InlineData("scores = [3, 1, 2]", "scores", false, "[1, 2, 3]")]
    [InlineData("scores = [3, 1, 2]", "scores", true, "[3, 2, 1]")]
    [InlineData("names = ['Linus', 'Ada', 'Grace']", "names", false, "['Ada', 'Grace', 'Linus']")]
    public async Task Sorts_by_the_requested_key_and_direction(
        string data,
        string collection,
        bool descending,
        string expectedOutput)
    {
        var code = PlanAndRender(
            [collection],
            new SortRequirement(
                "ordered",
                "entry",
                new SemanticNameExpression(collection),
                new SemanticNameExpression("entry"),
                descending),
            new OutputRequirement(new SemanticNameExpression("ordered")));

        await AssertProgramOutputAsync(data, code, expectedOutput);
    }

    [Theory]
    [InlineData("prices = {'apple': 1.5, 'banana': 0.75}", "prices", "price", "1.5\n0.75")]
    [InlineData("counts = {'Ada': 3, 'Grace': 5}", "counts", "count", "3\n5")]
    public async Task Iterates_dictionary_pairs_with_varied_keys_and_values(
        string data,
        string collection,
        string valueName,
        string expectedOutput)
    {
        var code = PlanAndRender(
            [collection],
            new DictionaryIterationRequirement(
                "product",
                valueName,
                new SemanticNameExpression(collection),
                [new OutputRequirement(new SemanticNameExpression(valueName))]));

        await AssertProgramOutputAsync(data, code, expectedOutput);
    }

    private static string PlanAndRender(
        ImmutableArray<string> collections,
        params ProgramRequirement[] requirements)
    {
        var program = new SemanticProgram([.. requirements])
        {
            Collections = [.. collections.Select(name =>
                new CollectionBinding(name, SemanticCollectionItemKind.Number))]
        };

        Assert.True(SemanticProgramPlanner.TryPlan(program, Pack, out var statements, out var error), error);
        return SemanticProgramPlanner.Render(statements);
    }

    private static async Task AssertProgramOutputAsync(
        string data,
        string code,
        string expectedOutput)
    {
        var program = data + Environment.NewLine + code;
        var startInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "python" : "python3",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(program);

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        var outputTask = process!.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var output = await outputTask;
        var error = await errorTask;

        Assert.True(process.ExitCode == 0, error + Environment.NewLine + program);
        Assert.Equal(
            expectedOutput + "\n",
            output.Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    private static GenerationLanguagePack LoadPack()
    {
        var loader = new GenerationLanguagePackLoader();
        Assert.True(loader.TryLoadEmbedded(out var packs, out var error), error);
        return packs["python"];
    }
}
