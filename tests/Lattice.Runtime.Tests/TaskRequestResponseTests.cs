using System.Diagnostics;
using Lattice.Client;
using Lattice.Runtime;
using Xunit;

namespace Lattice.Runtime.Tests;

public sealed class TaskRequestResponseTests
{
    [Fact]
    public async Task Explains_contrastive_learning_from_the_semantic_graph_for_a_beginner()
    {
        const string input = "Can you explain contrastive learning in machine learning in simple terms for someone new to the field of ML?";
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(input);

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, utterance.Kind);
        Assert.Contains("positive pair", utterance.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("negative pair", utterance.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("embedding", utterance.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("similar", utterance.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("different", utterance.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("image", utterance.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(utterance.SupportingEvidence);
        Assert.NotEmpty(utterance.SupportingEvidence.SelectMany(evidence => evidence.Sources));
    }

    [Fact]
    public async Task Does_not_invent_an_explanation_for_an_unmodeled_concept()
    {
        const string input = "Can you explain quantum foam in simple terms for someone new to physics?";
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(input);

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.InformationUnavailable, utterance.Kind);
    }

    [Fact]
    public async Task Builds_a_practical_search_optimization_learning_path_from_graph_knowledge()
    {
        const string input = "How can I learn to optimize my webpage for search engines?";
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(input);

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, utterance.Kind);
        Assert.Contains("indexing", utterance.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("search intent", utterance.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("mobile", utterance.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("performance", utterance.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("internal links", utterance.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("impressions", utterance.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(utterance.SupportingEvidence);
        Assert.NotEmpty(utterance.SupportingEvidence.SelectMany(evidence => evidence.Sources));
    }

    [Fact]
    public async Task Does_not_invent_a_learning_path_for_an_unmodeled_skill()
    {
        const string input = "How can I learn to optimize a spacecraft trajectory for interstellar travel?";
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(input);

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.InformationUnavailable, utterance.Kind);
    }

    [Theory]
    [InlineData("Write a Hello World program in Python.", "Hello, world!", "print(\"Hello, world!\")")]
    [InlineData("write a python hello world program", "Hello, world!", "print(\"Hello, world!\")")]
    [InlineData("Can you generate a program in Python to display Hello, world?", "Hello, world!", "print(\"Hello, world!\")")]
    [InlineData("Please build a script using Python that says hello world.", "Hello, world!", "print(\"Hello, world!\")")]
    [InlineData("I want to create a Python program that prints Hello World.", "Hello, world!", "print(\"Hello, world!\")")]
    [InlineData("Could you write a simple Python script to display Hello, world?", "Hello, world!", "print(\"Hello, world!\")")]
    [InlineData("Create a Python program that prints 'A second message!'.", "A second message!", "print(\"A second message!\")")]
    [InlineData("Write a script in Python to display 'Keep this: punctuation, please!'.", "Keep this: punctuation, please!", "print(\"Keep this: punctuation, please!\")")]
    [InlineData("Create a Python program that prints 'She said \"hello\"!'.", "She said \"hello\"!", "print(\"She said \\\"hello\\\"!\")")]
    public async Task Generates_and_executes_python_from_reusable_generation_knowledge(
        string input,
        string expectedOutput,
        string expectedCode)
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(input);

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, utterance.Kind);
        Assert.Empty(utterance.SupportingEvidence);
        Assert.Empty(utterance.OpposingEvidence);

        var generatedCode = ExtractPythonCode(utterance.Message);
        Assert.Equal(expectedCode, generatedCode);
        await AssertPythonPrints(generatedCode, expectedOutput);
    }

    private static string ExtractPythonCode(string message)
    {
        var fence = new string((char)96, 3);
        var opening = fence + "python";
        var lines = message.Split(Environment.NewLine, StringSplitOptions.None);
        var start = Array.IndexOf(lines, opening);
        if (start < 0)
        {
            throw new InvalidOperationException("The response did not contain a Python code block.");
        }

        var end = Array.IndexOf(lines, fence, start + 1);
        if (end <= start)
        {
            throw new InvalidOperationException("The Python code block was not closed.");
        }

        return string.Join(Environment.NewLine, lines[(start + 1)..end]);
    }

    private static async Task AssertPythonPrints(string code, string expectedOutput)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "python" : "python3",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(code);

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        var outputTask = process!.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await process.WaitForExitAsync(timeout.Token);

        Assert.Equal(0, process.ExitCode);
        Assert.Equal(expectedOutput + Environment.NewLine, await outputTask);
        Assert.Equal(string.Empty, await errorTask);
    }

    [Fact]
    public async Task Generates_a_Blender_script_that_replaces_matching_objects_with_point_lights()
    {
        const string input = "How would I create a script for blender that replaces all of objects with a specific name with a point light?";
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(input);

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, utterance.Kind);
        Assert.Contains("## replace matching Blender objects with point lights", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("### Script", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("```python", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("### Steps", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("- Snapshot scene objects", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("- Create point-light data", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("### Safety", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("Save a copy of your Blender file before running", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("target_name = \"YourObjectName\"", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("bpy.data.objects", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("bpy.data.lights.new", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("type=\"POINT\"", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("obj.matrix_world.translation.copy()", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("collection.objects.link(light_obj)", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("bpy.data.objects.remove(obj, do_unlink=True)", utterance.Message, StringComparison.Ordinal);
        Assert.Contains(
            "    if name_pattern.fullmatch(obj.name) is None:" + Environment.NewLine +
            "        continue" + Environment.NewLine +
            "    light_data = bpy.data.lights.new",
            utterance.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "    if collections:" + Environment.NewLine +
            "        for collection in collections:" + Environment.NewLine +
            "            collection.objects.link(light_obj)" + Environment.NewLine +
            "    else:" + Environment.NewLine +
            "        bpy.context.scene.collection.objects.link(light_obj)" + Environment.NewLine +
            "    bpy.data.objects.remove(obj, do_unlink=True)",
            utterance.Message,
            StringComparison.Ordinal);
        Assert.Contains(@"\.\d+", utterance.Message, StringComparison.Ordinal);
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
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, utterance.Kind);
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
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, utterance.Kind);
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
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, utterance.Kind);
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
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, utterance.Kind);
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
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, utterance.Kind);
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
    [Fact]
    public async Task Suggests_beginner_astrophotography_activities_from_the_semantic_graph()
    {
        const string input = "I want to start doing astrophotography as a hobby, any suggestions what could i do?";
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(input);

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, utterance.Kind);
        Assert.Contains("constellations", utterance.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("wide-field", utterance.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Moon", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("deep-sky", utterance.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("first night-sky photo session", utterance.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("choose a bright target", utterance.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("remote shutter release", utterance.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tracking mount", utterance.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Never view the Sun", utterance.Message, StringComparison.Ordinal);
        Assert.NotEmpty(utterance.SupportingEvidence);
        Assert.NotEmpty(utterance.SupportingEvidence.SelectMany(evidence => evidence.Sources));
    }

    [Fact]
    public async Task Does_not_invent_hobby_activities_for_an_unmodeled_hobby()
    {
        const string input = "I want to start doing underwater basket weaving as a hobby, any suggestions?";
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(input);

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.InformationUnavailable, utterance.Kind);
    }
}
