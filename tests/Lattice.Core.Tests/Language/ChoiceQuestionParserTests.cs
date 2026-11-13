using Lattice.Core.Language;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Language;

public sealed class ChoiceQuestionParserTests
{
    private readonly LceParser parser = new();

    [Fact]
    public void Parses_species_choice_question_with_unpunctuated_alternatives()
    {
        const string input = "Which is a species of fish? Tope or Rope";

        var result = parser.ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.Single(result.Utterances);
        var question = Assert.IsType<ChoiceQuestionFormula>(utterance.Meaning);

        Assert.Equal(UtteranceKind.Question, utterance.Kind);
        Assert.Equal("fish", question.Category.Value);
        Assert.Equal(new[] { "tope", "rope" }, question.Choices.Select(choice => choice.Value));
        Assert.Equal(input, utterance.SourceText);
        Assert.Equal("ask(choice(fish, tope, rope))", SemanticFormatter.Format(utterance));
    }

    [Theory]
    [InlineData("Which is a species of fish? Tope or Tope")]
    [InlineData("Which is a species of fish?")]
    [InlineData("Which is a species of fish? Tope, Rope")]
    public void Rejects_incomplete_or_ambiguous_choice_questions(string input)
    {
        var result = parser.ParseText(input);

        Assert.False(result.IsSuccess);
    }
}
