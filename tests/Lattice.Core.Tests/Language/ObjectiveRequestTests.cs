using Lattice.Core.Language;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Language;

public sealed class ObjectiveRequestTests
{
    private readonly LceParser parser = new();

    [Theory]
    [InlineData("I have a free Saturday, a limited budget, and no car. I would prefer somewhere quiet and outdoors. Help me put together a good day.", "put together a good day")]
    [InlineData("I'm looking for a laptop that will last a long time. I don't need gaming performance, but I care about screen quality, reliability, and repairability. Help me compare these three options. My budget is $1,000.", "compare these three options")]
    public void Parses_objectives_and_preserves_the_complete_request(string input, string expectedGoal)
    {
        var result = parser.ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(UtteranceKind.Objective, utterance.Kind);
        Assert.Equal(expectedGoal, Assert.IsType<ObjectiveFormula>(utterance.Meaning).Goal);
        Assert.Equal(input, utterance.SourceText);
    }

    [Fact]
    public void Does_not_treat_a_statement_about_helping_as_an_objective()
    {
        var result = parser.ParseText("Fido helps Mira.");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(UtteranceKind.Assertion, Assert.Single(result.Utterances).Kind);
    }
}
