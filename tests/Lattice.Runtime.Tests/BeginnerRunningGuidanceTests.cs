using Xunit;
using Lattice.Client;
using Lattice.Runtime;

namespace Lattice.Runtime.Tests;

public sealed class BeginnerRunningGuidanceTests
{
    [Fact]
    public async Task Builds_beginner_running_guidance_from_ordered_practices_and_purposes()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(
            "How do I start running?",
            TestContext.Current.CancellationToken);

        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, answer.Kind);
        Assert.Equal(
            "ask(world-model-beginner-guidance(running))",
            answer.Meaning);
        Assert.StartsWith(
            "To start running, use a gradual beginner routine:",
            answer.Message!,
            StringComparison.Ordinal);
        Assert.Contains("Five-minute brisk-walking warm-up", answer.Message!, StringComparison.Ordinal);
        Assert.Contains("One-minute easy jogs alternating with 90-second walks", answer.Message!, StringComparison.Ordinal);
        Assert.Contains("Conversational jogging pace", answer.Message!, StringComparison.Ordinal);
        Assert.Contains("Two or three weekly sessions", answer.Message!, StringComparison.Ordinal);
        Assert.Contains("recovery days", answer.Message!, StringComparison.Ordinal);
        Assert.Contains("Gradual increases in jogging intervals", answer.Message!, StringComparison.Ordinal);
        Assert.Contains("comfortable running shoes", answer.Message!, StringComparison.Ordinal);
        Assert.Contains("chest pain", answer.Message!, StringComparison.Ordinal);
        Assert.Equal(1, answer.Message!.Split("chest pain", StringSplitOptions.None).Length - 1);
        Assert.Contains("Stop and seek medical help", answer.Message!, StringComparison.Ordinal);
        Assert.Contains("purpose: muscle readiness", answer.Message!, StringComparison.Ordinal);
        Assert.Equal(5, answer.Message!.Split('\n').Count(line =>
            line.Length > 0 && char.IsDigit(line[0]) && line.Contains('.')));
        Assert.Contains(
            answer.SupportingEvidence!,
            evidence => evidence.Sources.Any(source =>
                source.SourceId.Contains(":has-first-beginner-step:", StringComparison.Ordinal)));
        Assert.Contains(
            answer.SupportingEvidence!,
            evidence => evidence.Sources.Any(source =>
                source.SourceId.Contains(":followed-by:", StringComparison.Ordinal)));
        Assert.Contains(
            answer.SupportingEvidence!,
            evidence => evidence.Sources.Any(source =>
                source.SourceId.Contains(":has-practice-purpose:", StringComparison.Ordinal)));
        Assert.Contains(
            answer.SupportingEvidence!,
            evidence => evidence.Sources.Any(source =>
                source.SourceId.Contains(":has-safety-guidance:", StringComparison.Ordinal)));
        Assert.Contains(
            answer.SupportingEvidence!,
            evidence => evidence.Sources.Any(source =>
                source.SourceId.Contains(":stop-and-seek-help-for:", StringComparison.Ordinal)));
        Assert.All(
            answer.SupportingEvidence!,
            evidence => Assert.Contains(
                evidence.Sources,
                source => source.Kind == LatticeKnowledgeSourceKind.FoundationalKnowledge));
    }

    [Theory]
    [InlineData("How do I start jogging?")]
    [InlineData("How do I start run?")]
    public async Task Resolves_running_synonyms_to_the_same_activity(string prompt)
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(prompt, TestContext.Current.CancellationToken);

        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, answer.Kind);
        Assert.Equal(
            "ask(world-model-beginner-guidance(running))",
            answer.Meaning);
    }

    [Fact]
    public async Task Does_not_invent_guidance_for_an_activity_missing_from_the_model()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(
            "How do I start swimming?",
            TestContext.Current.CancellationToken);

        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.InformationUnavailable, answer.Kind);
        Assert.Empty(answer.SupportingEvidence);
    }
}
