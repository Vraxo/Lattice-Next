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

}
