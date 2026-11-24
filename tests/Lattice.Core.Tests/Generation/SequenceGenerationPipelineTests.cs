using System.Collections.Immutable;
using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class SequenceGenerationPipelineTests
{
    [Theory]
    [InlineData("Hello, world!", "print(\"Hello, world!\")")]
    [InlineData("A second message!", "print(\"A second message!\")")]
    [InlineData("He said \"hello\".", "print(\"He said \\\"hello\\\".\")")]
    [InlineData("First line\nSecond line", "print(\"First line\\nSecond line\")")]
    [InlineData("Backslash \\", "print(\"Backslash \\\\\")")]
    public void Uses_the_universal_sequence_engine_with_different_semantic_bindings(
        string value,
        string expected)
    {
        var loader = new GenerationLanguagePackLoader();
        Assert.True(loader.TryLoadEmbedded(out var packs, out var loadError), loadError);

        var goal = new GenerationGoal(
            "write-standard-output",
            "text",
            value,
            ImmutableArray<string>.Empty);

        var generated = new SequenceGenerationPipeline().TryGenerate(
            packs["python"],
            goal,
            out var result,
            out var error);

        Assert.True(generated, error);
        Assert.Equal(expected, result!.Text);
        Assert.Equal("python-print", result.SelectedOperationId);
        Assert.False(result.SearchLimitReached);
    }

    [Fact]
    public void Renders_structured_collection_items_with_the_language_pack()
    {
        var loader = new GenerationLanguagePackLoader();
        Assert.True(loader.TryLoadEmbedded(out var packs, out var loadError), loadError);

        var goal = new GenerationGoal(
            "emit-loop-header",
            "collection-loop-header",
            "collection-loop",
            ImmutableArray<string>.Empty)
        {
            Bindings = ImmutableDictionary<string, string>.Empty
                .Add("variable", "entry"),
            CollectionBindings = ImmutableDictionary<string, SemanticCollection>.Empty
                .Add("iterable", new SemanticCollection([
                    new SemanticCollectionItem(SemanticCollectionItemKind.Number, "2"),
                    new SemanticCollectionItem(SemanticCollectionItemKind.Text, "red, blue"),
                    new SemanticCollectionItem(SemanticCollectionItemKind.Number, "-1.5")
                ]))
        };

        Assert.True(new SequenceGenerationPipeline().TryGenerate(
            packs["python"],
            goal,
            out var result,
            out var error), error);
        Assert.Equal("for entry in [2, \"red, blue\", -1.5]:", result!.Text);
        Assert.Equal("python-bound-for-collection-loop", result.SelectedOperationId);
    }

    [Fact]
    public void Rejects_a_goal_without_a_known_semantic_operation()
    {
        var loader = new GenerationLanguagePackLoader();
        Assert.True(loader.TryLoadEmbedded(out var packs, out var loadError), loadError);

        var goal = new GenerationGoal(
            "change-object-transform",
            "transform",
            "origin",
            ImmutableArray<string>.Empty);

        Assert.False(new SequenceGenerationPipeline().TryGenerate(
            packs["python"],
            goal,
            out var result,
            out var error));

        Assert.Null(result);
        Assert.Contains("No known operation", error, StringComparison.Ordinal);
    }
    [Fact]
    public void Resolves_multiple_named_bindings_from_a_semantic_goal()
    {
        var pack = new GenerationLanguagePack(
            1,
            "python",
            "assignment",
            [
                new GenerationProduction(
                    "assignment",
                    [
                        new GenerationSymbol("binding", "target"),
                        new GenerationSymbol("terminal", " = "),
                        new GenerationSymbol("binding", "expression")
                    ])
            ],
            [
                new GenerationBinding("target", "goal-binding", "none") { Key = "target" },
                new GenerationBinding("expression", "goal-binding", "none") { Key = "expression" }
            ],
            [
                new GenerationOperation(
                    "python-assignment",
                    "=",
                    "assign-variable",
                    "assignment",
                    [])
                {
                    StartSymbol = "assignment"
                }
            ],
            new GenerationStringLiteralFormat("\"", "\"", []));

        var goal = new GenerationGoal(
            "assign-variable",
            "assignment",
            "expression",
            ImmutableArray<string>.Empty)
        {
            Bindings = ImmutableDictionary<string, string>.Empty
                .Add("target", "objects")
                .Add("expression", "list(bpy.data.objects)")
        };

        Assert.True(
            new SequenceGenerationPipeline().TryGenerate(
                pack,
                goal,
                out var result,
                out var error),
            error);
        Assert.Equal("objects = list(bpy.data.objects)", result!.Text);
    }

    [Fact]
    public void Emits_structured_statements_from_generic_goal_bindings()
    {
        var loader = new GenerationLanguagePackLoader();
        Assert.True(loader.TryLoadEmbedded(out var packs, out var loadError), loadError);
        var pipeline = new SequenceGenerationPipeline();
        var pack = packs["python"];

        string Generate(
            string effect,
            string argumentKind,
            string argumentValue,
            params (string Key, string Value)[] bindings)
        {
            var goal = new GenerationGoal(
                effect,
                argumentKind,
                argumentValue,
                ImmutableArray<string>.Empty)
            {
                Bindings = bindings.ToImmutableDictionary(
                    binding => binding.Key,
                    binding => binding.Value,
                    StringComparer.Ordinal)
            };

            Assert.True(pipeline.TryGenerate(pack, goal, out var result, out var error), error);
            return result!.Text;
        }

        Assert.Equal(
            "objects = list(bpy.data.objects)",
            Generate("emit-assignment", "assignment", "assignment",
                ("target", "objects"),
                ("expression", "list(bpy.data.objects)")));
        Assert.Equal(
            "for obj in objects:",
            Generate("emit-loop-header", "loop-header", "loop",
                ("variable", "obj"),
                ("iterable", "objects")));
        Assert.Equal(
            "if collections:",
            Generate("emit-conditional-header", "condition", "condition",
                ("condition", "collections")));
        Assert.Equal(
            "import bpy",
            Generate("emit-import", "module-name", "bpy",
                ("module", "bpy")));
        Assert.Equal(
            "bpy.data.objects.remove(obj, do_unlink=True)",
            Generate("emit-call", "call", "remove",
                ("function", "bpy.data.objects.remove"),
                ("arguments", "obj, do_unlink=True")));
        Assert.Equal("else:", Generate("emit-else", "none", "none"));
        Assert.Equal("continue", Generate("emit-continue", "none", "none"));
    }

}
