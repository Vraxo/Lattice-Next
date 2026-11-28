using System.Text.Json;
using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class InlineApiExpressionTests
{
    private static readonly GenerationLanguagePack Pack = LoadPack();

    [Fact]
    public void Composes_api_values_as_index_receivers_keys_and_arguments()
    {
        const string source = "api('pygame-keys')[api('pygame-key-w')] == True";
        Assert.True(SemanticExpressionParser.TryParseBooleanSyntax(source, Pack.InputSyntax!, out var expression));
        var comparison = Assert.IsType<SemanticBinaryExpression>(expression);
        var index = Assert.IsType<SemanticSubscriptExpression>(comparison.Left);
        Assert.Equal("pygame-keys", Assert.IsType<SemanticApiExpression>(index.Target).Contract);
        Assert.Equal("pygame-key-w", Assert.IsType<SemanticApiExpression>(index.Key).Contract);
        Assert.Empty(SemanticExpressionAnalysis.GetReferencedNames(expression!));
        Assert.True(SemanticExpressionRenderer.TryRender(expression!, Pack, out var rendered));
        Assert.Equal("pygame.key.get_pressed()[pygame.K_w] == True", rendered);
        Assert.True(SemanticExpressionParser.TryParseValue("api('pygame-event', api('pygame-quit-event')).type", Pack.InputSyntax!, out expression));
        Assert.True(SemanticExpressionRenderer.TryRender(expression!, Pack, out rendered));
        Assert.Equal("pygame.event.Event(pygame.QUIT).type", rendered);
    }

    [Fact]
    public void Input_notation_is_declared_and_does_not_replace_ordinary_calls()
    {
        var syntax = Pack.InputSyntax! with { ApiReferenceFunctionName = "contract" };
        Assert.True(SemanticExpressionParser.TryParseValue("contract('pygame-key-s')", syntax, out var value));
        Assert.IsType<SemanticApiExpression>(value);
        Assert.True(SemanticExpressionParser.TryParseValue("api('pygame-key-s')", syntax, out value));
        Assert.IsType<SemanticFunctionCallExpression>(value);
        Assert.True(SemanticExpressionParser.TryParseValue("api('pygame-key-s')", syntax with { ApiReferenceFunctionName = null }, out value));
        Assert.IsType<SemanticFunctionCallExpression>(value);
    }

    [Theory]
    [InlineData("api()")]
    [InlineData("api(name)")]
    [InlineData("api(12)")]
    [InlineData("api('')")]
    public void Rejects_missing_or_nonliteral_contract_names(string source) =>
        Assert.False(SemanticExpressionParser.TryParseValue(source, Pack.InputSyntax!, out _));

    [Theory]
    [InlineData("api('undeclared-contract')")]
    [InlineData("api('pygame-key-w', 1)")]
    [InlineData("api('pygame-event')")]
    public void Rendering_rejects_undeclared_or_wrong_arity_calls(string source)
    {
        Assert.True(SemanticExpressionParser.TryParseValue(source, Pack.InputSyntax!, out var value));
        Assert.False(SemanticExpressionRenderer.TryRender(value!, Pack, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("two words")]
    [InlineData("True")]
    [InlineData("and")]
    public void Loader_rejects_ambiguous_or_invalid_input_names(string name)
    {
        var pack = Pack with { InputSyntax = Pack.InputSyntax! with { ApiReferenceFunctionName = name } };
        Assert.False(new GenerationLanguagePackLoader().TryLoad(
            JsonSerializer.Serialize(pack, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }), out _, out _));
    }

    [Theory]
    [InlineData("pygame-key-w", "pygame.K_w")]
    [InlineData("pygame-key-s", "pygame.K_s")]
    [InlineData("pygame-key-up", "pygame.K_UP")]
    [InlineData("pygame-key-down", "pygame.K_DOWN")]
    public void Normal_interpretation_preserves_inline_control_checks(string contract, string code)
    {
        var request = $"Write a Python program using Pygame. Set position to 10 then when api('pygame-keys')[api('{contract}')] == True then set position to position + 3.";
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var error), error);
        Assert.True(SequenceInputFactExtractor.TryExtract(knowledge!, request, out var facts, out error), error);
        Assert.True(SemanticProgramPlanner.TryGenerateProgram(facts, Pack, out var generated, out _, out error), error);
        Assert.Contains($"pygame.key.get_pressed()[{code}] == True", generated, StringComparison.Ordinal);
    }

    private static GenerationLanguagePack LoadPack()
    {
        Assert.True(new GenerationLanguagePackLoader().TryLoadEmbedded(out var packs, out var error), error);
        return packs["python"];
    }
}
