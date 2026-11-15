using Xunit;
using Lattice.Client;
using Lattice.Runtime;

namespace Lattice.Runtime.Tests;

public sealed class MobilePhoneHarmQuestionTests
{
    [Fact]
    public async Task Explains_human_impacts_from_effects_and_their_mechanisms()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(
            "Why mobile is bad for human",
            TestContext.Current.CancellationToken);

        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, answer.Kind);
        Assert.Equal(
            "ask(world-model-harmfulness(mobile-phone-use, person))",
            answer.Meaning);
        Assert.StartsWith(
            "Mobile phone use is not inherently bad for people, but excessive or poorly timed use can contribute to",
            answer.Message!,
            StringComparison.Ordinal);
        Assert.Contains("sleep disruption", answer.Message!, StringComparison.Ordinal);
        Assert.Contains("attention fragmentation", answer.Message!, StringComparison.Ordinal);
        Assert.Contains("eye strain", answer.Message!, StringComparison.Ordinal);
        Assert.Contains("neck and back discomfort", answer.Message!, StringComparison.Ordinal);
        Assert.Contains("reduced physical activity", answer.Message!, StringComparison.Ordinal);
        Assert.Contains("social interaction disruption", answer.Message!, StringComparison.Ordinal);
        Assert.Contains("notification", answer.Message!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("bent-neck posture", answer.Message!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            answer.SupportingEvidence,
            evidence => evidence.Sources.Any(source =>
                source.SourceId.Contains(":can-contribute-to:", StringComparison.Ordinal)));
        Assert.Contains(
            answer.SupportingEvidence,
            evidence => evidence.Sources.Any(source =>
                source.SourceId.Contains(":can-arise-through:", StringComparison.Ordinal)));
        Assert.All(
            answer.SupportingEvidence,
            evidence => Assert.Contains(
                evidence.Sources,
                source => source.Kind == LatticeKnowledgeSourceKind.FoundationalKnowledge));
    }

    [Theory]
    [InlineData("Why mobile phones are bad for humans?")]
    [InlineData("Why smartphone use is bad for people?")]
    [InlineData("Why is mobile bad for humans?")]
    [InlineData("Why are mobile phones bad for humans?")]
    [InlineData("Why are smartphones bad for people?")]
    public async Task Resolves_common_mobile_phone_and_human_aliases(string prompt)
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(prompt, TestContext.Current.CancellationToken);

        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, answer.Kind);
        Assert.Equal(
            "ask(world-model-harmfulness(mobile-phone-use, person))",
            answer.Meaning);
    }
}
