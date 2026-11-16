using Lattice.Client;
using Lattice.Runtime;
using Xunit;

namespace Lattice.Runtime.Tests;

public sealed class TaskRequestResponseTests
{
    [Fact]
    public async Task Generates_a_Blender_script_that_replaces_matching_objects_with_point_lights()
    {
        const string input = "How would I create a script for blender that replaces all of objects with a specific name with a point light?";
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(input);

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, utterance.Kind);
        Assert.Contains("## Blender object replacement", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("### Script", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("```python", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("### What it does", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("- Finds every object", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("**Before running:**", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("target_name = \"YourObjectName\"", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("bpy.data.objects", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("bpy.data.lights.new", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("type=\"POINT\"", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("obj.matrix_world.translation.copy()", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("collection.objects.link(light_obj)", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("bpy.data.objects.remove(obj, do_unlink=True)", utterance.Message, StringComparison.Ordinal);
        Assert.Contains(".001", utterance.Message, StringComparison.Ordinal);
        Assert.NotEmpty(utterance.SupportingEvidence);
        Assert.NotEmpty(utterance.SupportingEvidence.SelectMany(evidence => evidence.Sources));
    }

    [Fact]
    public async Task Does_not_generate_a_Blender_script_for_an_unmodeled_task()
    {
        const string input = "How would I create a script for blender that renames every object?";
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(input);

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.InformationUnavailable, utterance.Kind);
    }

    [Fact]
    public async Task Brainstorms_distinct_beverage_can_uses_from_structured_affordances()
    {
        const string input = "What are 5 potential uses for an empty La Croix can?";
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(input);

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, utterance.Kind);
        Assert.Contains("Pen holder", utterance.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Small-parts organizer", utterance.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Seedling starter pot", utterance.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Dried-flower display", utterance.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Percussion shaker", utterance.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("drainage holes", utterance.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("candle holder", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("sharp rim", utterance.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(utterance.SupportingEvidence);
        Assert.NotEmpty(utterance.SupportingEvidence.SelectMany(evidence => evidence.Sources));
    }

    [Fact]
    public async Task Does_not_invent_uses_for_an_unmodeled_empty_object()
    {
        const string input = "What are 5 potential uses for an empty cardboard box?";
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(input);

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.InformationUnavailable, utterance.Kind);
    }

    [Fact]
    public async Task Explains_manual_transmission_operation_from_structured_procedure_knowledge()
    {
        const string input = "How do you operate a car with a manual transmission?";
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(input);

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, utterance.Kind);
        Assert.Contains("left foot", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("right foot", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("bite point", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("neutral", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("upshift", utterance.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("downshift", utterance.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("parking brake", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("qualified instructor", utterance.Message, StringComparison.Ordinal);
        Assert.NotEmpty(utterance.SupportingEvidence);
        Assert.NotEmpty(utterance.SupportingEvidence.SelectMany(evidence => evidence.Sources));
    }

    [Fact]
    public async Task Does_not_invent_an_operating_procedure_for_an_unknown_transmission()
    {
        const string input = "How do you operate a car with an automatic transmission?";
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(input);

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.InformationUnavailable, utterance.Kind);
    }

    [Fact]
    public async Task Plans_a_three_day_road_trip_from_structured_route_knowledge()
    {
        const string input = "Plan for a three day road trip for May long weekend from Westfield, New Jersey";
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(input);

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, utterance.Kind);
        Assert.Contains("Memorial Day", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("Day 1", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("Day 2", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("Day 3", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("Morristown National Historical Park", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("Princeton University", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("Lambertville", utterance.Message, StringComparison.Ordinal);
        Assert.NotEmpty(utterance.SupportingEvidence);
        Assert.NotEmpty(utterance.SupportingEvidence.SelectMany(evidence => evidence.Sources));
    }

    [Fact]
    public async Task Does_not_invent_a_road_trip_route_for_an_unknown_origin()
    {
        const string input = "Plan for a three day road trip for May long weekend from Atlantis";
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(input);

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.InformationUnavailable, utterance.Kind);
    }

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
        Assert.Contains(
            utterance.SupportingEvidence,
            evidence => evidence.Sources.Any(source =>
                source.SourceId == "core-world-model:relation:labor-market-monopsony-power:has-research-reference:azar-marinescu-2024-publication"));
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
        Assert.DoesNotContain("https://", utterance.Message, StringComparison.Ordinal);
        Assert.Contains(
            utterance.SupportingEvidence,
            evidence => evidence.Sources.Any(source =>
                source.SourceId == "core-world-model:relation:idaes:instance-of:process-systems-engineering-framework"));
    }
}
