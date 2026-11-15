using Lattice.Core.Language;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Language;

public sealed class LHopitalApplicationsQuestionTests
{
    private readonly LceParser parser = new();

    [Fact]
    public void Parses_everyday_application_question_as_a_semantic_application_query()
    {
        const string input = "How can L’Hopital’s Rule be used in day to day life?";

        var result = parser.ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        var question = Assert.IsType<SemanticWorldModelQuestionFormula>(Assert.Single(result.Utterances).Meaning);
        Assert.Equal(SemanticWorldModelQuestionKind.Applications, question.Kind);
        Assert.Equal("l’hopital’s-rule", question.Subject);
    }

    [Fact]
    public void Parses_everyday_as_an_alternative_to_day_to_day()
    {
        const string input = "How can L'Hopital's Rule be used in everyday life?";

        var result = parser.ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        var question = Assert.IsType<SemanticWorldModelQuestionFormula>(Assert.Single(result.Utterances).Meaning);
        Assert.Equal(SemanticWorldModelQuestionKind.Applications, question.Kind);
        Assert.Equal("l'hopital's-rule", question.Subject);
    }
}
