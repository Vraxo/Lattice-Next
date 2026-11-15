using Lattice.Core.Language;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Language;

public sealed class CompanyIntroductionQuestionTests
{
    private readonly LceParser parser = new();

    [Theory]
    [InlineData("I started a new company named Greystone Inc. What do we do?", "Greystone Inc.")]
    [InlineData("I founded a company named Northstar Labs. What does it do?", "Northstar Labs")]
    [InlineData("I launched a new company named Cedar Works LLC. What does the company do?", "Cedar Works LLC")]
    public void Parses_a_company_introduction_as_a_business_ideation_question(
        string input,
        string expectedCompany)
    {
        var result = parser.ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.Single(result.Utterances);
        var question = Assert.IsType<SemanticWorldModelQuestionFormula>(utterance.Meaning);

        Assert.Equal(UtteranceKind.Question, utterance.Kind);
        Assert.Equal(SemanticWorldModelQuestionKind.CreativeIdeas, question.Kind);
        Assert.Equal("business-ideas", question.Subject);
        Assert.Equal(expectedCompany, question.OtherSubject);
        Assert.Equal(input, utterance.SourceText);
    }
}
