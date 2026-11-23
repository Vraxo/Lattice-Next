using System.Collections.Immutable;
using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class ComparisonOperatorNormalizerTests
{
    [Theory]
    [InlineData("greater than", ">")]
    [InlineData("more than", ">")]
    [InlineData("GREATER   THAN", ">")]
    [InlineData("greater than or equal to", ">=")]
    [InlineData("at least", ">=")]
    [InlineData("less than", "<")]
    [InlineData("below", "<")]
    [InlineData("less than or equal to", "<=")]
    [InlineData("at most", "<=")]
    [InlineData("equal to", "==")]
    [InlineData("equals", "==")]
    [InlineData("is equal to", "==")]
    [InlineData("not equal to", "!=")]
    [InlineData("is not equal to", "!=")]
    [InlineData("!=", "!=")]
    public void Normalizes_comparison_language_to_an_operator(string input, string expected)
    {
        Assert.True(ComparisonOperatorNormalizer.TryNormalize(input, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData("approximately")]
    [InlineData("greater")]
    [InlineData("not")]
    public void Rejects_unknown_or_incomplete_comparisons(string input)
    {
        Assert.False(ComparisonOperatorNormalizer.TryNormalize(input, out var actual));
        Assert.Equal(string.Empty, actual);
    }

    [Theory]
    [InlineData("greater than", ">")]
    [InlineData("at least", ">=")]
    [InlineData("below", "<")]
    [InlineData("at most", "<=")]
    [InlineData("equals", "==")]
    [InlineData("not equal to", "!=")]
    public void Extractor_normalizes_comparison_operator_captures(string phrase, string expected)
    {
        var capture = new SequenceInputFactCapture(
            "program.comparison",
            "operator",
            ImmutableArray<string>.Empty,
            ImmutableArray<string>.Empty)
        {
            Normalization = "comparisonOperator"
        };
        var rule = new SequenceInputFactRule(
            "capture-comparison",
            ImmutableArray.Create("comparison"),
            "program.comparison",
            Captures: ImmutableArray.Create(capture));
        var knowledge = new SequenceKnowledgeBase(
            ImmutableArray<SequenceTokenRule>.Empty,
            InputRules: ImmutableArray.Create(rule));

        Assert.True(SequenceInputFactExtractor.TryExtract(
            knowledge,
            $"comparison {phrase}",
            out var facts,
            out var error), error);

        Assert.Equal(expected, facts.Single().Values["operator"]);
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
            Normalization = "comparisonOperator"
        };
        var rule = new SequenceInputFactRule(
            "capture-comparison",
            ImmutableArray.Create("comparison"),
            "program.comparison",
            Captures: ImmutableArray.Create(capture));
        var knowledge = new SequenceKnowledgeBase(
            ImmutableArray<SequenceTokenRule>.Empty,
            InputRules: ImmutableArray.Create(rule));

        Assert.False(SequenceInputFactExtractor.TryExtract(
            knowledge,
            "comparison approximately",
            out var facts,
            out var error));

        Assert.Empty(facts);
        Assert.Contains("unsupported comparison operator", error, StringComparison.Ordinal);
    }
}
