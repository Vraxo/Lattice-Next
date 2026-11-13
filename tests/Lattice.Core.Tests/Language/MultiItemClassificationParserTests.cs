using Lattice.Core.Language;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Language;

public sealed class MultiItemClassificationParserTests
{
    private readonly LceParser parser = new();

    [Fact]
    public void Parses_items_and_alternative_categories_as_a_single_classification_request()
    {
        const string input = "Identify which instrument is string or percussion: Cantaro, Gudok";

        var result = parser.ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.Single(result.Utterances);
        var classification = Assert.IsType<MultiItemClassificationFormula>(utterance.Meaning);

        Assert.Equal(UtteranceKind.Question, utterance.Kind);
        Assert.Equal("instrument", classification.ItemType.Value);
        Assert.Equal(new[] { "string", "percussion" }, classification.Categories.Select(category => category.Value));
        Assert.Equal(new[] { "cantaro", "gudok" }, classification.Items.Select(item => item.Value));
        Assert.Equal(input, utterance.SourceText);
        Assert.Equal(
            "ask(classify(instrument, categories(string, percussion), items(cantaro, gudok)))",
            SemanticFormatter.Format(utterance));
    }

    [Theory]
    [InlineData("Identify which instrument is string or string: Cantaro, Gudok")]
    [InlineData("Identify which instrument is string or percussion: Cantaro")]
    [InlineData("Identify which instrument is string or percussion: Cantaro, Cantaro")]
    public void Rejects_ambiguous_or_incomplete_classification_requests(string input)
    {
        var result = parser.ParseText(input);

        Assert.False(result.IsSuccess);
    }
}
