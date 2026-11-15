using Lattice.Core.Language;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Language;

public sealed class CompanyIntroductionQuestionTests
{
    private readonly LceParser parser = new();

    [Theory]
    [InlineData("I started a new company named Greystone Inc. What do we do?", "greystone-inc")]
    [InlineData("I founded a company named Northstar Labs. What does it do?", "northstar-labs")]
    [InlineData("I launched a new company named Cedar Works LLC. What does the company do?", "cedar-works-llc")]
    public void Parses_a_company_introduction_followed_by_a_business_question(
        string input,
        string expectedCompany)
    {
        var result = parser.ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.Single(result.Utterances);
        var question = Assert.IsType<SemanticRelationQueryFormula>(utterance.Meaning);

        Assert.Equal(UtteranceKind.Question, utterance.Kind);
        Assert.Equal(expectedCompany, question.Subject);
        Assert.Equal("do", question.Verb);
        Assert.Equal(input, utterance.SourceText);
    }
}
