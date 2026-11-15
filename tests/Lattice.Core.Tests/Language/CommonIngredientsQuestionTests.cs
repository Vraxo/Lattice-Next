using Lattice.Core.Language;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Language;

public sealed class CommonIngredientsQuestionTests
{
    private readonly LceParser parser = new();

    [Fact]
    public void Parses_common_ingredients_question_for_a_named_dish()
    {
        const string input = "What are common ingredients of a full english breakfast?";

        var result = parser.ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.Single(result.Utterances);
        var question = Assert.IsType<SemanticWorldModelQuestionFormula>(utterance.Meaning);
        Assert.Equal(SemanticWorldModelQuestionKind.Ingredients, question.Kind);
        Assert.Equal("full-english-breakfast", question.Subject);
        Assert.Equal(
            "ask(world-model-ingredients(full-english-breakfast))",
            SemanticFormatter.Format(utterance));
    }
}
