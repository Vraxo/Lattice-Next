using Lattice.Core.Language;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Language;

public sealed class OpenAssistantQuestionTests
{
    private readonly LceParser parser = new();

    [Fact]
    public void Parses_a_compound_comparison_recommendation_and_examples_request()
    {
        const string input = "What is the difference between open assistant and ChatGPT? Why should i use Open Assistant? And can you give me some examples of advanced chatbots that are similar to Open Assistant?";

        var result = parser.ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(3, result.Utterances.Length);

        var comparison = Assert.IsType<SemanticWorldModelQuestionFormula>(result.Utterances[0].Meaning);
        Assert.Equal(SemanticWorldModelQuestionKind.Compare, comparison.Kind);
        Assert.Equal("open-assistant", comparison.Subject);
        Assert.Equal("chatgpt", comparison.OtherSubject);

        var recommendation = Assert.IsType<SemanticWorldModelQuestionFormula>(result.Utterances[1].Meaning);
        Assert.Equal(SemanticWorldModelQuestionKind.WhyUse, recommendation.Kind);
        Assert.Equal("open-assistant", recommendation.Subject);

        var examples = Assert.IsType<SemanticWorldModelQuestionFormula>(result.Utterances[2].Meaning);
        Assert.Equal(SemanticWorldModelQuestionKind.ListExamples, examples.Kind);
        Assert.Equal("advanced-chatbot", examples.Subject);
        Assert.Equal(5, examples.Limit);
    }
}
