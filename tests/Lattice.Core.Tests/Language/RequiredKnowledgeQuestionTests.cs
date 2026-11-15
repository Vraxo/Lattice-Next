using Lattice.Core.Language;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Language;

public sealed class RequiredKnowledgeQuestionTests
{
    private readonly LceParser parser = new();

    [Fact]
    public void Parses_how_much_knowledge_is_required_as_a_semantic_question()
    {
        const string input = "How much music theory knowledge is required to compose music?";

        var result = parser.ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        var question = Assert.IsType<SemanticWorldModelQuestionFormula>(Assert.Single(result.Utterances).Meaning);
        Assert.Equal(SemanticWorldModelQuestionKind.RequiredKnowledge, question.Kind);
        Assert.Equal("compose-music", question.Subject);
        Assert.Equal("music-theory-knowledge", question.Concept);
    }
}
