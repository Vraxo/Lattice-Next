using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class ApiArgumentRequestTests
{
    [Theory]
    [InlineData("surface", "160", "120", "saving")]
    [InlineData("canvas", "240", "180", "save")]
    public void Projects_request_arguments_and_result_names(string name, string width, string height, string verb)
    {
        var program = Interpret($"Write a Python program using Pygame. Initialize Pygame. Invoke api pygame-window with arguments ([{width}, {height}]) {verb} result as {name}.");
        var binding = Assert.IsType<AssignmentRequirement>(program.Requirements[2]);
        Assert.Equal(name, binding.VariableName);
        var api = Assert.IsType<SemanticApiExpression>(binding.Value);
        Assert.Equal("pygame-window", api.Contract);
        var size = Assert.IsType<SemanticListLiteralExpression>(Assert.Single(api.Arguments));
        Assert.Equal(new SemanticNumberExpression(width), size.Items[0]);
        Assert.Equal(new SemanticNumberExpression(height), size.Items[1]);
    }

    [Fact]
    public void Associates_each_clause_with_its_own_invocation()
    {
        var program = Interpret("Write a Python program using Pygame. Invoke api pygame-clock saving result as timer then invoke api pygame-tick with arguments (timer, 60) saving result as elapsed.");
        var clock = Assert.IsType<SemanticApiExpression>(Assert.IsType<AssignmentRequirement>(program.Requirements[1]).Value);
        Assert.Empty(clock.Arguments);
        var tick = Assert.IsType<SemanticApiExpression>(Assert.IsType<AssignmentRequirement>(program.Requirements[2]).Value);
        Assert.Equal("pygame-tick", tick.Contract);
        Assert.Equal(new SemanticNameExpression("timer"), tick.Arguments[0]);
        Assert.Equal(new SemanticNumberExpression("60"), tick.Arguments[1]);
    }

    [Fact]
    public void Parses_nested_lists_and_quoted_capture_keywords_as_values()
    {
        var program = Interpret("Write a Python program that invoke api declared-contract with arguments ([['then', 'saving'], []], 1 + 2) saving result as value.");
        var api = Assert.IsType<SemanticApiExpression>(Assert.IsType<AssignmentRequirement>(Assert.Single(program.Requirements)).Value);
        var outer = Assert.IsType<SemanticListLiteralExpression>(api.Arguments[0]);
        var inner = Assert.IsType<SemanticListLiteralExpression>(outer.Items[0]);
        Assert.Equal(new SemanticTextExpression("then"), inner.Items[0]);
        Assert.Equal(new SemanticTextExpression("saving"), inner.Items[1]);
        Assert.Empty(Assert.IsType<SemanticListLiteralExpression>(outer.Items[1]).Items);
        Assert.IsType<SemanticBinaryExpression>(api.Arguments[1]);
    }

    [Theory]
    [InlineData("Write a Python program with arguments (1).")]
    [InlineData("Write a Python program saving result as value.")]
    [InlineData("Write a Python program that invoke api pygame-window. With arguments ([160, 120]).")]
    [InlineData("Write a Python program that invoke api pygame-window with arguments ([160, 120).")]
    [InlineData("Write a Python program that invoke api pygame-clock saving result as first then saving result as second.")]
    [InlineData("Write a Python program that invoke api pygame-clock saving result as bad name.")]
    public void Rejects_orphan_malformed_or_duplicate_clauses(string request)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var error), error);
        Assert.False(SequenceInputFactExtractor.TryExtractProgram(knowledge!, request, out _, out error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Theory]
    [InlineData("Write a Python program that invoke api pygame-window with arguments (160, 120) saving result as surface.")]
    [InlineData("Write a Python program that invoke api pygame-tick with arguments (missing, 60) saving result as elapsed.")]
    public void Planner_rejects_wrong_arity_and_unavailable_argument_values(string request)
    {
        var program = Interpret(request);
        Assert.True(new GenerationLanguagePackLoader().TryLoadEmbedded(out var packs, out var error), error);
        Assert.False(SemanticProgramPlanner.TryPlan(program, packs["python"], out var statements, out _));
        Assert.Empty(statements);
    }

    [Fact]
    public void Argument_and_list_delimiters_are_selected_by_the_language_pack()
    {
        Assert.True(new GenerationLanguagePackLoader().TryLoadEmbedded(out var packs, out var error), error);
        var syntax = packs["python"].InputSyntax! with
        {
            ListLiteralDelimiter = new GenerationDelimiterPair("«", "»")
        };
        Assert.True(SemanticExpressionParser.TryParse("arguments", "(«1, 2», 'then')", syntax, out var expression));
        var values = Assert.IsType<SemanticArgumentsExpression>(expression);
        Assert.Equal(2, Assert.IsType<SemanticListLiteralExpression>(values.Values[0]).Items.Length);
        Assert.False(SemanticExpressionParser.TryParseValue("[1]", syntax with { ListLiteralDelimiter = null }, out _));
        Assert.False(SemanticExpressionParser.TryParse("arguments", "(1,)", syntax, out _));
        Assert.False(SemanticExpressionParser.TryParse("arguments", "(1) trailing", syntax, out _));
    }

    private static SemanticProgram Interpret(string request)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var error), error);
        Assert.True(SequenceInputFactExtractor.TryExtractProgram(knowledge!, request, out var program, out error), error);
        return Assert.IsType<SemanticProgram>(program);
    }
}
