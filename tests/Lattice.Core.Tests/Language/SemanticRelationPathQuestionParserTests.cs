using Lattice.Core.Language;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Language;

public sealed class SemanticRelationPathQuestionParserTests
{
    [Fact]
    public void Parses_a_relation_path_question_into_a_generic_semantic_formula()
    {
        var result = new LceParser().Parse(
            "How is the piano hammers connected to the piano soundboard?");

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.IsType<SemanticUtterance>(result.Utterance);
        var question = Assert.IsType<SemanticWorldModelQuestionFormula>(utterance.Meaning);

        Assert.Equal(UtteranceKind.Question, utterance.Kind);
        Assert.Equal(SemanticWorldModelQuestionKind.RelationPath, question.Kind);
        Assert.Equal("piano-hammers", question.Subject);
        Assert.Equal("piano-soundboard", question.OtherSubject);
        Assert.Equal(
            "ask(world-model-path(piano-hammers, piano-soundboard))",
            SemanticFormatter.Format(utterance));
    }
}
