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
}
