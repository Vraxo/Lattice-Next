using Xunit;
using Lattice.Client;
using Lattice.Runtime;

namespace Lattice.Runtime.Tests;

public sealed class HeadquartersContributionQuestionTests
{
    [Fact]
    public async Task Separates_the_financial_gift_from_the_citys_property_contribution()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(
            "Who gave the UN the land in NY to build their HQ",
            TestContext.Current.CancellationToken);

        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, answer.Kind);
        Assert.Equal(
            "ask(world-model-contribution(united-nations, land-parcel, new-york-city, headquarters-site))",
            answer.Meaning);
        Assert.Equal(
            "John D. Rockefeller Jr. funded the acquisition of the United Nations headquarters site. City of New York contributed additional land and property rights to the United Nations headquarters site.",
            answer.Message);
        Assert.Contains(
            answer.SupportingEvidence,
            evidence => evidence.Sources.Any(source =>
                source.SourceId.Contains(":funded-acquisition-of:", StringComparison.Ordinal)));
        Assert.Contains(
            answer.SupportingEvidence,
            evidence => evidence.Sources.Any(source =>
                source.SourceId.Contains(":contributed-property-to:", StringComparison.Ordinal)));
        Assert.Contains(
            answer.SupportingEvidence,
            evidence => evidence.Sources.Any(source =>
                source.SourceId.Contains(":has-headquarters-in:", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Resolves_full_names_and_expanded_headquarters_wording()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(
            "Who gave the United Nations the land in New York City to build its headquarters?",
            TestContext.Current.CancellationToken);

        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, answer.Kind);
        Assert.Contains("John D. Rockefeller Jr.", answer.Message!, StringComparison.Ordinal);
        Assert.Contains("City of New York", answer.Message!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Who gave the UN the land in Boston to build their HQ?")]
    [InlineData("Who gave the UN the water in NY to build their HQ?")]
    [InlineData("Who gave the UN the land in NY to build their stadium?")]
    public async Task Abstains_when_the_recipient_resource_location_or_facility_does_not_match(
        string prompt)
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(prompt, TestContext.Current.CancellationToken);

        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.InformationUnavailable, answer.Kind);
        Assert.Empty(answer.SupportingEvidence);
    }
}
