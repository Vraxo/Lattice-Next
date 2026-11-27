using System.Collections.Immutable;
using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class ComparisonOperatorNormalizerTests
{
    [Theory]
    [InlineData("greater than", SemanticExpressionOperator.GreaterThan)]
    [InlineData("more than", SemanticExpressionOperator.GreaterThan)]
    [InlineData("GREATER   THAN", SemanticExpressionOperator.GreaterThan)]
    [InlineData("greater than or equal to", SemanticExpressionOperator.GreaterThanOrEqual)]
    [InlineData("at least", SemanticExpressionOperator.GreaterThanOrEqual)]
    [InlineData("less than", SemanticExpressionOperator.LessThan)]
    [InlineData("below", SemanticExpressionOperator.LessThan)]
    [InlineData("less than or equal to", SemanticExpressionOperator.LessThanOrEqual)]
    [InlineData("at most", SemanticExpressionOperator.LessThanOrEqual)]
    [InlineData("equal", SemanticExpressionOperator.Equal)]
    [InlineData("equal to", SemanticExpressionOperator.Equal)]
    [InlineData("equals", SemanticExpressionOperator.Equal)]
    [InlineData("is equal to", SemanticExpressionOperator.Equal)]
    [InlineData("not equal to", SemanticExpressionOperator.NotEqual)]
    [InlineData("is not equal to", SemanticExpressionOperator.NotEqual)]
    public void Parses_comparison_phrases_to_language_neutral_operator_kinds(
        string input,
        SemanticExpressionOperator expected)
    {
        Assert.True(ComparisonOperatorNormalizer.TryParse(input, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("more than", SemanticExpressionOperator.GreaterThan)]
    [InlineData("below", SemanticExpressionOperator.LessThan)]
    [InlineData("equal", SemanticExpressionOperator.Equal)]
    public void Parses_verbal_comparisons_as_typed_expressions(
        string phrase,
        SemanticExpressionOperator expected)
    {
        var syntax = LoadPythonPack().InputSyntax!;
        Assert.True(SemanticExpressionParser.TryParse(
            "boolean", $"score {phrase} 5", syntax, out var expression));
        Assert.Equal(
            new SemanticBinaryExpression(
                expected,
                new SemanticNameExpression("score"),
                new SemanticNumberExpression("5")),
            expression);
    }

    [Theory]
    [InlineData("")]
    [InlineData("approximately")]
    [InlineData("greater")]
    [InlineData("not")]
    [InlineData(">")]
    public void Rejects_unknown_or_language_specific_operator_spellings(string input)
    {
        Assert.False(ComparisonOperatorNormalizer.TryParse(input, out _));
    }

    [Theory]
    [InlineData("greater than", SemanticExpressionOperator.GreaterThan)]
    [InlineData("at least", SemanticExpressionOperator.GreaterThanOrEqual)]
    [InlineData("below", SemanticExpressionOperator.LessThan)]
    [InlineData("at most", SemanticExpressionOperator.LessThanOrEqual)]
    [InlineData("equals", SemanticExpressionOperator.Equal)]
    [InlineData("not equal to", SemanticExpressionOperator.NotEqual)]
    public void Extractor_materializes_comparison_operator_captures_as_typed_values(
        string phrase,
        SemanticExpressionOperator expected)
    {
        var capture = new SequenceInputFactCapture(
            "program.comparison",
            "operator",
            ImmutableArray<string>.Empty,
            ImmutableArray<string>.Empty)
        {
            SemanticExpressionKind = "comparisonOperator"
        };
        var rule = new SequenceInputFactRule(
            "capture-comparison",
            ImmutableArray.Create("comparison"),
            "program.comparison",
            Captures: ImmutableArray.Create(capture));
        var knowledge = new SequenceKnowledgeBase(
            InputRules: ImmutableArray.Create(rule));
        var pack = LoadPythonPack();

        Assert.True(SequenceInputFactExtractor.TryExtract(
            knowledge,
            $"comparison {phrase}",
            pack,
            out var facts,
            out var error), error);

        var comparison = Assert.Single(facts);
        Assert.Empty(comparison.Values);
        Assert.Equal(expected, comparison.ComparisonOperators["operator"]);
    }

    [Fact]
    public void Extractor_rejects_unsupported_comparison_operator_captures()
    {
        var capture = new SequenceInputFactCapture(
            "program.comparison",
            "operator",
            ImmutableArray<string>.Empty,
            ImmutableArray<string>.Empty)
        {
            SemanticExpressionKind = "comparisonOperator"
        };
        var rule = new SequenceInputFactRule(
            "capture-comparison",
            ImmutableArray.Create("comparison"),
            "program.comparison",
            Captures: ImmutableArray.Create(capture));
        var knowledge = new SequenceKnowledgeBase(
            InputRules: ImmutableArray.Create(rule));

        Assert.False(SequenceInputFactExtractor.TryExtract(
            knowledge,
            "comparison approximately",
            LoadPythonPack(),
            out var facts,
            out var error));

        Assert.Empty(facts);
        Assert.Contains("unsupported comparison operator", error, StringComparison.Ordinal);
    }

    private static GenerationLanguagePack LoadPythonPack()
    {
        var loader = new GenerationLanguagePackLoader();
        Assert.True(loader.TryLoadEmbedded(out var packs, out var error), error);
        return packs["python"];
    }
}
