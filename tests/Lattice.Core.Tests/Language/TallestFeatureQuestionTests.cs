using Lattice.Core.Language;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Language;

public sealed class TallestFeatureQuestionTests
{
    private readonly LceParser parser = new();

    [Fact]
    public void Parses_scoped_tallest_feature_question()
    {
        const string input = "What is the tallest mountain in the continental US?";

        var result = parser.ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        var question = Assert.IsType<SemanticWorldModelQuestionFormula>(Assert.Single(result.Utterances).Meaning);
        Assert.Equal(SemanticWorldModelQuestionKind.HighestDataValue, question.Kind);
        Assert.Equal("mountain", question.Subject);
        Assert.Equal("mountain-elevation-feet", question.Concept);
        Assert.Equal("continental-us", question.TargetType);
        Assert.Equal("feet above sea level", question.OtherSubject);
    }
}
