using System.Collections.Immutable;
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

    /// <summary>
    /// The modelled task's steps in render order: a step, then the body it opens, then its alternative branch.
    /// The knowledge declares the shape, so the order and the nesting both come from its assertions.
    /// </summary>
    private static ImmutableArray<string> OrderedSteps(
        SemanticWorldModelReasoner reasoner,
        string subject,
        string relation)
    {
        var builder = ImmutableArray.CreateBuilder<string>();
        Collect(subject, relation);
        return builder.ToImmutable();

        void Collect(string from, string through)
        {
            foreach (var assertion in reasoner.GetRelations(from, through))
            {
                builder.Add(assertion.Object);
                Collect(assertion.Object, "step-has-body");
                Collect(assertion.Object, "step-has-alternative-branch");
            }
        }
    }

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
        var steps = OrderedSteps(reasoner, task!.Id, "has-generation-step");
        Assert.Equal(18, steps.Length);
        Assert.Equal(steps.Length, steps.Distinct(StringComparer.Ordinal).Count());

        var apiReferences = steps
            .SelectMany(step => reasoner.GetRelations(step, "step-references-api"))
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
        var stepLabels = OrderedSteps(reasoner, task!.Id, "has-generation-step")
            .Select(step => model.FindConcept(step, SemanticConceptKind.Individual)?.Label)
            .ToArray();

        Assert.Equal("Import the Blender API", stepLabels[0]);
        Assert.Equal("Remove the original object", stepLabels[^1]);
        Assert.All(stepLabels, label => Assert.False(string.IsNullOrWhiteSpace(label)));

        var steps = OrderedSteps(reasoner, task.Id, "has-generation-step");
        Assert.All(
            steps,
            step =>
            {
                Assert.Single(reasoner.GetRelations(step, "step-has-generation-effect"));
                Assert.Single(reasoner.GetRelations(step, "step-has-argument-kind"));
            });

        // The three API-reaching calls are declared as API operations with the contract they reach, so their
        // effect is the api-call family rather than the generic call family the other families share.
        var apiCallSteps = steps
            .Where(step =>
            {
                var effect = reasoner.GetRelations(step, "step-has-generation-effect");
                var label = effect.Length == 1
                    ? model.FindConcept(effect[0].Object, SemanticConceptKind.Individual)?.Label
                    : null;
                return label is not null && label.Contains("api call", StringComparison.Ordinal);
            })
            .ToArray();
        Assert.Equal(3, apiCallSteps.Length);
        Assert.All(
            apiCallSteps,
            step => Assert.NotEmpty(reasoner.GetRelations(step, "step-references-api")));

        // Sixteen steps carry bindings; the two control-only steps (skip and else) carry none.
        var boundSteps = steps
            .Where(step => !reasoner.GetRelations(step, "step-has-binding").IsDefaultOrEmpty)
            .ToArray();
        Assert.Equal(16, boundSteps.Length);
        Assert.All(
            boundSteps.SelectMany(step => reasoner.GetRelations(step, "step-has-binding")),
            binding => Assert.Single(reasoner.GetRelations(binding.Object, "binding-has-value")));
    }

    [Fact]
    public void Derives_every_indentation_from_where_a_step_sits()
    {
        var model = SemanticWorldModelResourceLoader.Load();
        var reasoner = new SemanticWorldModelReasoner(model);

        var task = model.FindConcept(ModelledTask, SemanticConceptKind.Individual);
        Assert.NotNull(task);

        // No effect names a depth: the block a step sits in is what makes its effect indented.
        var steps = OrderedSteps(reasoner, task!.Id, "has-generation-step");
        var effects = steps
            .SelectMany(step => reasoner.GetRelations(step, "step-has-generation-effect"))
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .ToArray();
        Assert.NotEmpty(effects);
        Assert.All(
            effects,
            effect => Assert.DoesNotContain(
                "indented",
                effect!.Label,
                StringComparison.OrdinalIgnoreCase));

        // The shape is the knowledge's: a loop holds a guard, a branch, and the removal; the branch holds a
        // nested loop and an alternative branch; the alternative branch holds the fallback link.
        Assert.Equal(
            new[]
            {
                "step-match-name",
                "step-create-light-data",
                "step-create-light-object",
                "step-copy-world-position",
                "step-snapshot-collections",
                "step-check-collections",
                "step-remove-original"
            },
            reasoner.GetRelations("step-iterate-objects", "step-has-body").Select(item => item.Object));
        Assert.Equal(
            new[] { "step-iterate-collections" },
            reasoner.GetRelations("step-check-collections", "step-has-body").Select(item => item.Object));
        Assert.Equal(
            new[] { "step-else" },
            reasoner.GetRelations("step-check-collections", "step-has-alternative-branch")
                .Select(item => item.Object));
        Assert.Equal(
            new[] { "step-fallback-link" },
            reasoner.GetRelations("step-else", "step-has-body").Select(item => item.Object));
        Assert.Equal(
            new[] { "step-link-collections" },
            reasoner.GetRelations("step-iterate-collections", "step-has-body").Select(item => item.Object));
        Assert.Equal(
            new[] { "step-continue-nonmatch" },
            reasoner.GetRelations("step-match-name", "step-has-body").Select(item => item.Object));
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

        // The answer cites the exact step bindings it read from the domain knowledge.
        Assert.Contains(
            utterance.SupportingEvidence.SelectMany(evidence => evidence.Sources),
            source => source.SourceId ==
                "core-world-model:relation:step-set-target-name:step-has-binding:" +
                "binding-step-set-target-name-string-value");
    }

    [Fact]
    public async Task Fills_the_object_name_and_light_kind_a_request_states()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        // The phrase fits a form the knowledge gives slots for, so the request's own words reach the script.
        var result = await session.SubmitAsync(
            "How would I create a script for blender that replaces all of objects named Cube with a sun light?");

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, utterance.Kind);
        Assert.Contains("target_name = \"Cube\"", utterance.Message, StringComparison.Ordinal);
        Assert.Contains("type=\"SUN\"", utterance.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("YourObjectName", utterance.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("type=\"POINT\"", utterance.Message, StringComparison.Ordinal);

        // Only the filled slots change; every other line is still the knowledge's own text.
        Assert.Contains(
            "name_pattern = re.compile(rf\"{re.escape(target_name)}(?:\\.\\d+)?$\")",
            utterance.Message,
            StringComparison.Ordinal);
        Assert.Contains("bpy.data.objects.remove(obj, do_unlink=True)", utterance.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Does_not_invent_a_light_kind_the_knowledge_does_not_declare()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "How would I create a script for blender that replaces all of objects named Cube with a strobe light?");

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.InformationUnavailable, utterance.Kind);
        Assert.DoesNotContain("```python", utterance.Message, StringComparison.Ordinal);
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
