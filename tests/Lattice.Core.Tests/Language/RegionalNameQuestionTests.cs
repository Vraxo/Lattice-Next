using Lattice.Core.Language;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Language;

public sealed class RegionalNameQuestionTests
{
    private readonly LceParser parser = new();

    [Fact]
    public void Parses_what_a_region_calls_a_term()
    {
        const string input = "In golf what do the Americans call an albatross";

        var result = parser.ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.Single(result.Utterances);
        var question = Assert.IsType<SemanticWorldModelQuestionFormula>(utterance.Meaning);
        Assert.Equal(SemanticWorldModelQuestionKind.RegionalName, question.Kind);
        Assert.Equal("albatross", question.Subject);
        Assert.Equal("american-english", question.TargetType);
        Assert.Equal(
            "ask(world-model-regional-name(albatross, in(american-english)))",
            SemanticFormatter.Format(utterance));
    }
}
