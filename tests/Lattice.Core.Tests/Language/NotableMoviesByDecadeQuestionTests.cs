using Lattice.Core.Language;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Language;

public sealed class NotableMoviesByDecadeQuestionTests
{
    private readonly LceParser parser = new();

    [Fact]
    public void Parses_best_movies_question_with_possessive_decade_spelling()
    {
        const string input = "What are some of the best comedy movies of the 1980's?";

        var result = parser.ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        var question = Assert.IsType<SemanticWorldModelQuestionFormula>(Assert.Single(result.Utterances).Meaning);
        Assert.Equal(SemanticWorldModelQuestionKind.NotableExamples, question.Kind);
        Assert.Equal("comedy", question.Subject);
        Assert.Equal("comedy-film-1980s", question.Concept);
        Assert.Equal("1980s", question.TargetType);
        Assert.Equal(12, question.Limit);
        Assert.Equal("ask(world-model-notable-examples(comedy-film-1980s, 12))", SemanticFormatter.Format(Assert.Single(result.Utterances)));
    }
}
