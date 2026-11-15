using Lattice.Core.Language;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Language;

public sealed class MissingListItemsClarificationTests
{
    private readonly LceParser parser = new();

    [Fact]
    public void Parses_unpunctuated_list_membership_request_with_missing_items()
    {
        const string input = "Tell me whether or not these foods are on EWG's dirty dozen list";

        var result = parser.ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        var question = Assert.IsType<SemanticWorldModelQuestionFormula>(Assert.Single(result.Utterances).Meaning);
        Assert.Equal(SemanticWorldModelQuestionKind.Clarification, question.Kind);
        Assert.Equal("foods", question.Subject);
        Assert.Equal("EWG's dirty dozen list", question.OtherSubject);
    }

    [Fact]
    public void Accepts_a_recognized_unpunctuated_final_sentence_after_a_complete_sentence()
    {
        const string input = "Fido is a dog. What is the difference between open assistant and ChatGPT";

        var result = parser.ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(2, result.Utterances.Length);
        Assert.Equal(UtteranceKind.Assertion, result.Utterances[0].Kind);
        Assert.Equal(UtteranceKind.Question, result.Utterances[1].Kind);
    }
}
