using Lattice.Client;
using Xunit;

namespace Lattice.Runtime.Tests;

public sealed class TypeInstanceListQuestionTests
{
    [Fact]
    public async Task Lists_instances_from_a_compositional_type_definition()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(
            "Give me the top 5 golf equipment company names.",
            TestContext.Current.CancellationToken);

        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, answer.Kind);
        Assert.Equal(
            "The model identifies 5 golf equipment companies: Acushnet Holdings Corp., Callaway Golf Company, PING Inc., Sumitomo Rubber Industries, Ltd., and TaylorMade Golf Company. The model does not rank them.",
            answer.Message);
        Assert.Equal(
            "ask(world-model-type-instances(golf-equipment-company, count=5))",
            answer.Meaning);
        var evidence = Assert.Single(answer.SupportingEvidence);
        Assert.Equal(LatticeEvidenceKind.Derived, evidence.Kind);
        Assert.Equal(
            5,
            evidence.Sources.Count(source =>
                source.SourceId?.Contains(":makes-equipment-for:", StringComparison.Ordinal) == true));
        Assert.Contains(
            evidence.Sources,
            source => source.SourceId?.Contains(":golf-sport-example:", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task Abstains_when_the_requested_type_is_not_in_the_model()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(
            "Give me the top 5 moon mining company names.",
            TestContext.Current.CancellationToken);

        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.InformationUnavailable, answer.Kind);
        Assert.Empty(answer.SupportingEvidence);
    }
}