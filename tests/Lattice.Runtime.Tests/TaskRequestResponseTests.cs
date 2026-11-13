using Lattice.Client;
using Lattice.Runtime;
using Xunit;

namespace Lattice.Runtime.Tests;

public sealed class TaskRequestResponseTests
{
    [Fact]
    public async Task Writes_a_research_grounded_monopsony_introduction_from_default_knowledge()
    {
        const string input = "Can you write a short introduction about the relevance of the term \"monopsony\" in economics? Please use examples related to potential monopsonies in the labour market and cite relevant research.";
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(input);

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, utterance.Kind);
        Assert.Contains("buyer market structure", utterance.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("wage-setting power", utterance.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("wage markdown", utterance.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("outside options", utterance.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rural town", utterance.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Manning (2021)", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("Azar and Marinescu (2024)", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("Yeh, Macaluso, and Hershbein (2022)", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("https://doi.org/10.1177/0019793920922499", utterance.Message, StringComparison.Ordinal);
        Assert.Contains(
            utterance.SupportingEvidence,
            evidence => evidence.Sources.Any(source =>
                source.SourceId == "core-world-model" &&
                source.SourceText == "Azar and Marinescu (2024), Monopsony Power in the Labor Market: From Theory to Policy."));
    }

    [Fact]
    public async Task Default_reference_metadata_does_not_become_objective_candidates()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync("Help me compare these options.");
        var status = await session.GetStatusAsync();

        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.ObjectiveReceived, Assert.Single(result.Utterances).Kind);
        Assert.Empty(status.ObjectiveEvaluation!.Candidates);
    }

    [Fact]
    public async Task Recommends_matching_open_source_python_process_and_optimization_tools()
    {
        const string input = "I would like to implement simulations of chemical unit operations using Python and use them to optimize certain outputs. Which open-source packages do you recommend for this purpose?";
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(input);

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, utterance.Kind);
        Assert.Contains("IDAES", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("BioSTEAM", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("Pyomo", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("unit-operation modeling", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("flowsheet optimization", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("algebraic optimization modeling", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("https://idaes-pse.readthedocs.io/en/stable/", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("https://biosteam.readthedocs.io/en/latest/", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("https://pyomo.readthedocs.io/en/stable/", utterance.Message, StringComparison.Ordinal);
        Assert.Contains(
            utterance.SupportingEvidence,
            evidence => evidence.Sources.Any(source =>
                source.SourceId == "core-world-model" &&
                source.SourceText.Contains("IDAES official overview", StringComparison.Ordinal)));
    }
}
