using System.Collections.Immutable;
using System.Text.Json;
using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class ApiCallExpressionTests
{
    private static readonly GenerationLanguagePack Pack = LoadPack() with
    {
        ApiExpressionFormats = [
            new("bounded", "min({0}, {1})") { ArgumentCount = 2 },
            new("values", "range({0}, {1})") { ArgumentCount = 2 },
            new("text", "str({0})") { ArgumentCount = 1 }]
    };

    [Fact]
    public void Renders_nested_arguments_without_reinterpreting_argument_text_as_a_format()
    {
        var expression = Api("bounded", new SemanticNumberExpression("3"),
            Api("bounded", new SemanticNumberExpression("7"), new SemanticNumberExpression("9")));
        Assert.True(SemanticExpressionRenderer.TryRender(expression, Pack, out var rendered));
        Assert.Equal("min(3, min(7, 9))", rendered);
        Assert.True(SemanticExpressionRenderer.TryRender(Api("text", new SemanticTextExpression("{0}")), Pack, out rendered));
        Assert.Equal("str(\"{0}\")", rendered);
    }

    [Theory]
    [InlineData("ceiling", "limit", "5", "3")]
    [InlineData("maximum", "bound", "8", "6")]
    public void Uses_results_in_assignments_loop_sources_and_other_call_arguments(
        string input, string result, string initial, string limit)
    {
        var program = new SemanticProgram([
            new AssignmentRequirement(input, new SemanticNumberExpression(initial)),
            new AssignmentRequirement(result, Api("bounded", new SemanticNameExpression(input), new SemanticNumberExpression(limit))),
            new IterationRequirement("item", Api("values", new SemanticNumberExpression("0"), new SemanticNameExpression(result)),
                [new OutputRequirement(Api("text", new SemanticNameExpression("item")))])]);
        Assert.True(SemanticProgramValidator.IsValid(program));
        Assert.True(SemanticProgramPlanner.TryPlan(program, Pack, out var statements, out var error), error);
        Assert.Equal(new[] { $"{input} = {initial}", $"{result} = min({input}, {limit})", $"for item in range(0, {result}):", "    print(str(item))" },
            statements.Select(statement => statement.Text));
    }

    [Fact]
    public void Tracks_dependencies_recursively_and_rejects_unbound_arguments()
    {
        var expression = Api("text", Api("bounded", new SemanticNameExpression("missing"), new SemanticNumberExpression("3")));
        Assert.Equal(["missing"], SemanticExpressionAnalysis.GetReferencedNames(expression));
        Assert.Empty(SemanticExpressionAnalysis.GetFunctionNames(expression));
        Assert.False(SemanticExpressionAnalysis.ReferencesOnlyAvailableNames(expression, []));
        Assert.False(SemanticProgramPlanner.TryPlan(new SemanticProgram([
            new AssignmentRequirement("result", expression)]), Pack, out var statements, out var error));
        Assert.Empty(statements);
        Assert.Contains("missing", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_undeclared_contracts_wrong_arity_and_malformed_arguments()
    {
        Assert.False(SemanticExpressionRenderer.TryRender(Api("missing"), Pack, out _));
        Assert.False(SemanticExpressionRenderer.TryRender(Api("text"), Pack, out _));
        Assert.False(SemanticExpressionRenderer.TryRender(Api("text", Api("missing")), Pack, out _));
        Assert.False(SemanticExpressionAnalysis.IsValid(new SemanticApiExpression("text") { Arguments = default }));
        Assert.False(SemanticExpressionAnalysis.IsValid(Api("text", new SemanticNameExpression(""))));
        Assert.False(SemanticExpressionAnalysis.IsValid(Api("text",
            Enumerable.Repeat<SemanticExpression>(new SemanticNumberExpression("1"), 9).ToArray())));
    }

    [Theory]
    [InlineData(1, "str({0})", true)]
    [InlineData(0, "constant", true)]
    [InlineData(1, "str({1})", false)]
    [InlineData(2, "str({0})", false)]
    [InlineData(-1, "constant", false)]
    [InlineData(9, "constant", false)]
    [InlineData(0, "str({0})", false)]
    [InlineData(1, "str({0", false)]
    public void Validates_declared_argument_counts_and_placeholders(int count, string format, bool expected)
    {
        var pack = LoadPack() with
        {
            ApiExpressionFormats = [new("test-contract", format) { ArgumentCount = count }]
        };
        var json = JsonSerializer.Serialize(pack, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Equal(expected, new GenerationLanguagePackLoader().TryLoad(json, out _, out _));
    }

    private static SemanticApiExpression Api(string contract, params SemanticExpression[] arguments) =>
        new(contract) { Arguments = [.. arguments] };

    private static GenerationLanguagePack LoadPack()
    {
        Assert.True(new GenerationLanguagePackLoader().TryLoadEmbedded(out var packs, out var error), error);
        return packs["python"];
    }
}
