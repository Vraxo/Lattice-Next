using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class ClauseLookaheadTests
{
    [Theory]
    [InlineData("Mark blue item then mark red item.", 1)]
    [InlineData("Mark red item then mark blue item.", 1)]
    [InlineData("Mark blue item. Mark red item.", 1)]
    [InlineData("Mark blue item then red item.", 0)]
    [InlineData("Mark blue item. Red item.", 0)]
    [InlineData("Mark 'red' item.", 0)]
    public void Lookahead_filters_occurrences_without_crossing_clause_boundaries(string request, int expected)
    {
        const string json = """
            {"formatVersion":1,"inputRules":[{"id":"local-marker","matchTokens":["mark"],"followingMatchTokens":["red","item"],"factId":"marker","valueId":"name","literalValue":"matched","repeatMatches":true,"searchAllSentences":true}]}
            """;
        Assert.True(SequenceKnowledgeBaseLoader.TryLoad(json, out var knowledge, out var error), error);
        Assert.Equal(expected > 0, SequenceInputFactExtractor.TryExtract(knowledge!, request, out var facts, out error));
        Assert.Equal(expected, facts.Count(fact => fact.Id == "marker"));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[1]")]
    [InlineData("[\"\"]")]
    [InlineData("\"red\"")]
    public void Rejects_malformed_lookahead_metadata(string value)
    {
        var json = "{\"formatVersion\":1,\"inputRules\":[{\"id\":\"marker\",\"matchTokens\":[\"mark\"],\"factId\":\"marker\",\"followingMatchTokens\":" + value + "}]}";
        Assert.False(SequenceKnowledgeBaseLoader.TryLoad(json, out _, out _));
    }
}
