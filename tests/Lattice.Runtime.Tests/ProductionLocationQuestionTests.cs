using Lattice.Client;

namespace Lattice.Runtime.Tests;

public sealed class ProductionLocationQuestionTests
{
    [Fact]
    public async Task Explains_why_film_productions_choose_Georgia_from_typed_advantages()
    {
        const string prompt = "Why are so many US Hollywood films made in the state of Georgia?";
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(prompt);

        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, answer.Kind);
        Assert.Equal(
            "ask(world-model-production-location-reasons(film, georgia))",
            answer.Meaning);
        Assert.Contains("20% base transferable tax credit", answer.Message, StringComparison.Ordinal);
        Assert.Contains("additional 10% promotional uplift", answer.Message, StringComparison.Ordinal);
        Assert.Contains("no annual cap", answer.Message, StringComparison.Ordinal);
        Assert.Contains("no sunset clause", answer.Message, StringComparison.Ordinal);
        Assert.Contains("4 million+ square feet", answer.Message, StringComparison.Ordinal);
        Assert.Contains("experienced local crews", answer.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("10,000+ filming locations", answer.Message, StringComparison.Ordinal);
        Assert.Contains("year-round filming", answer.Message, StringComparison.Ordinal);
        Assert.Contains("lower operating costs", answer.Message, StringComparison.Ordinal);
        Assert.Contains("Camera Ready", answer.Message, StringComparison.Ordinal);
        var evidence = Assert.Single(answer.SupportingEvidence);
        Assert.Equal(LatticeEvidenceKind.Derived, evidence.Kind);
        Assert.Contains(
            evidence.Sources,
            source => source.SourceId.Contains(":georgia:has-advantage:tax-credit-advantage", StringComparison.Ordinal));
        Assert.Contains(
            evidence.Sources,
            source => source.SourceId.Contains(":tax-credit-advantage:advantage-has-detail:base-credit-detail", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Why are so many spacecraft made in the state of Georgia?")]
    [InlineData("Why are so many US Hollywood films made in the state of Alabama?")]
    public async Task Abstains_when_the_activity_or_production_location_is_unmodeled(string prompt)
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(prompt);

        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.InformationUnavailable, answer.Kind);
    }
}
