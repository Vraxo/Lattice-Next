using Lattice.Client;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Runtime.Tests;

/// <summary>
/// Verifies that a script request for a modelled application is answered from domain knowledge through the
/// one program-generation entry point: the knowledge supplies the steps, their API contracts, and their
/// bindings, the language pack supplies the syntax, and no application-specific route exists beside it.
/// </summary>
public sealed class BlenderScriptGenerationTests
{
    private const string ModelledTask = "replaces-all-of-objects-with-a-specific-name-with-a-point-light";

    private static readonly string[] ModelledApis =
    [
        "api-object-datablocks",
        "api-object-name",
        "api-light-datablocks",
        "api-object-construction",
        "api-world-matrix",
        "api-object-collections",
        "api-collection-link",
        "api-object-removal",
        "blender-api-scene-collection-link"
    ];

    [Fact]
    public void Keeps_the_reusable_Blender_api_and_scene_vocabulary()
    {
        var model = SemanticWorldModelResourceLoader.Load();
        var reasoner = new SemanticWorldModelReasoner(model);

        foreach (var api in ModelledApis)
        {
            Assert.NotNull(model.FindConcept(api, SemanticConceptKind.Individual));
            Assert.True(reasoner.IsInstanceOf(api, "blender-api"), api);
            Assert.True(reasoner.IsInstanceOf(api, "application-programming-interface"), api);
        }

        Assert.NotNull(model.FindConcept("blender", SemanticConceptKind.Individual));

        var task = model.FindConcept(ModelledTask, SemanticConceptKind.Individual);
        Assert.NotNull(task);
        var steps = reasoner.GetRelations(task!.Id, "has-generation-step");
        Assert.Equal(18, steps.Length);
        Assert.Equal(
            steps.Length,
            steps.Select(step => step.Object).Distinct(StringComparer.Ordinal).Count());

        var apiReferences = steps
            .SelectMany(step => reasoner.GetRelations(step.Object, "step-references-api"))
            .ToArray();
        Assert.Equal(10, apiReferences.Length);
        Assert.All(
            apiReferences,
            reference => Assert.True(
                reasoner.IsInstanceOf(reference.Object, "application-programming-interface"),
                reference.Object));

        Assert.DoesNotContain(
            model.Concepts,
            concept => concept.Id.StartsWith("blender-codegen", StringComparison.Ordinal));
    }

    [Fact]
    public void Keeps_every_modelled_step_ordered_and_realizable()
    {
        var model = SemanticWorldModelResourceLoader.Load();
        var reasoner = new SemanticWorldModelReasoner(model);

        var task = model.FindConcept(ModelledTask, SemanticConceptKind.Individual);
        Assert.NotNull(task);
        var stepLabels = reasoner.GetRelations(task!.Id, "has-generation-step")
            .Select(step => model.FindConcept(step.Object, SemanticConceptKind.Individual)?.Label)
            .ToArray();

        Assert.Equal("Import the Blender API", stepLabels[0]);
        Assert.Equal("Remove the original object", stepLabels[^1]);
        Assert.All(stepLabels, label => Assert.False(string.IsNullOrWhiteSpace(label)));

        var steps = reasoner.GetRelations(task.Id, "has-generation-step");
        Assert.All(
            steps,
            step =>
            {
                Assert.Single(reasoner.GetRelations(step.Object, "step-has-generation-effect"));
                Assert.Single(reasoner.GetRelations(step.Object, "step-has-argument-kind"));
                Assert.NotEmpty(reasoner.GetRelations(step.Object, "step-has-binding"));
            });
    }

    [Fact]
    public async Task Generates_the_modelled_script_from_domain_knowledge()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "How would I create a script for blender that replaces all of objects with a specific name with a point light?");

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, utterance.Kind);

        // Both lines exist only in the domain knowledge and the language pack, never in shared code.
        Assert.Contains("target_name = \"YourObjectName\"", utterance.Message, StringComparison.Ordinal);
        Assert.Contains(
            "name_pattern = re.compile(rf\"{re.escape(target_name)}(?:\\.\\d+)?$\")",
            utterance.Message,
            StringComparison.Ordinal);

        Assert.Contains(
            utterance.SupportingEvidence.SelectMany(evidence => evidence.Sources),
            source => source.SourceId.StartsWith(
                "blender-object-replacement-script:",
                StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("How would I create a script for blender that renames every object?")]
    [InlineData("How would I create a script for Maya that creates a cube?")]
    public async Task Generates_nothing_when_the_knowledge_models_no_steps(string prompt)
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(prompt);

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.InformationUnavailable, utterance.Kind);
        Assert.DoesNotContain("```python", utterance.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("bpy", utterance.Message, StringComparison.Ordinal);
    }
}
