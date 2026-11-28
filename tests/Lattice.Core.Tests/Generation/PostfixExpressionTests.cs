using System.Text.Json;
using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class PostfixExpressionTests
{
    private static readonly GenerationLanguagePack Pack = LoadPack();

    [Fact]
    public void Preserves_member_index_call_and_arithmetic_precedence()
    {
        Assert.True(SemanticExpressionParser.TryParseValue("items[index + 1].position.x * -2", Pack.InputSyntax!, out var expression));
        var product = Assert.IsType<SemanticBinaryExpression>(expression);
        var x = Assert.IsType<SemanticMemberAccessExpression>(product.Left);
        Assert.Equal("x", x.Member);
        var position = Assert.IsType<SemanticMemberAccessExpression>(x.Target);
        var item = Assert.IsType<SemanticSubscriptExpression>(position.Target);
        Assert.Equal(new SemanticNameExpression("items"), item.Target);
        Assert.IsType<SemanticBinaryExpression>(item.Key);
        Assert.Equal(["items", "index"], SemanticExpressionAnalysis.GetReferencedNames(expression!));
        Assert.True(SemanticExpressionRenderer.TryRender(expression!, Pack, out var rendered));
        Assert.Equal("items[index + 1].position.x * -2", rendered);
    }

    [Theory]
    [InlineData("text.strip().upper()[0]")]
    [InlineData("records['key'][index].name")]
    [InlineData("make(value).position.x")]
    [InlineData("(left + right).real")]
    public void Composes_postfix_nodes_using_existing_renderers(string source)
    {
        Assert.True(SemanticExpressionParser.TryParseValue(source, Pack.InputSyntax!, out var expression));
        Assert.True(SemanticExpressionRenderer.TryRender(expression!, Pack, out var rendered));
        Assert.Equal(source.Replace("'key'", "\"key\"", StringComparison.Ordinal), rendered);
    }

    [Fact]
    public void Uses_language_declared_member_and_index_tokens()
    {
        var syntax = Pack.InputSyntax! with
        {
            MemberAccessFormat = new GenerationMemberAccessFormat("::"),
            SubscriptDelimiter = new GenerationDelimiterPair("«", "»")
        };
        var pack = Pack with { InputSyntax = syntax, SubscriptFormat = "{0}«{1}»" };
        Assert.True(SemanticExpressionParser.TryParseValue("items«key»::value", syntax, out var expression));
        Assert.True(SemanticExpressionRenderer.TryRender(expression!, pack, out var rendered));
        Assert.Equal("items«key»::value", rendered);
        Assert.False(SemanticExpressionParser.TryParseValue("items[key].value", syntax, out _));
        Assert.False(SemanticExpressionParser.TryParseValue("items[key]", syntax with { SubscriptDelimiter = null }, out _));
        Assert.False(SemanticExpressionParser.TryParseValue("item.value", Pack.InputSyntax! with { MemberAccessFormat = null }, out _));
    }

    [Theory]
    [InlineData("items[]")]
    [InlineData("items[1")]
    [InlineData("items[1, 2]")]
    [InlineData("items[1:2]")]
    [InlineData("item.")]
    [InlineData("item..value")]
    [InlineData("item.2")]
    [InlineData("item.method(1,)")]
    [InlineData("item[1] trailing")]
    public void Rejects_malformed_or_unsupported_postfix_syntax(string source)
    {
        Assert.False(SemanticExpressionParser.TryParseValue(source, Pack.InputSyntax!, out _));
    }

    [Fact]
    public void Rejects_index_delimiters_that_conflict_with_call_or_grouping()
    {
        var pack = Pack with
        {
            InputSyntax = Pack.InputSyntax! with { SubscriptDelimiter = new GenerationDelimiterPair("(", ")") }
        };
        var json = JsonSerializer.Serialize(pack, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.False(new GenerationLanguagePackLoader().TryLoad(json, out _, out _));
    }

    private static GenerationLanguagePack LoadPack()
    {
        Assert.True(new GenerationLanguagePackLoader().TryLoadEmbedded(out var packs, out var error), error);
        return packs["python"];
    }
}
