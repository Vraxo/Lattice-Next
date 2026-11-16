using System.Collections.Immutable;
using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class SequenceGeneratorTests
{
    [Fact]
    public void Expands_the_declarative_grammar_and_selects_an_operation_by_its_effect()
    {
        var loader = new GenerationLanguagePackLoader();
        Assert.True(loader.TryLoadEmbedded(out var packs, out var loadError), loadError);
        Assert.True(packs.TryGetValue("python", out var pack));

        var goal = new GenerationGoal(
            "write-standard-output",
            "text",
            "Hello, world!",
            ImmutableArray<string>.Empty);

        var generated = new SequenceGenerator().TryGenerate(
            pack!,
            goal,
            out var result,
            out var generationError);

        Assert.True(generated, generationError);
        Assert.Equal("print(\"Hello, world!\")", result!.Text);
        Assert.Equal(new[] { "print", "(", "\"Hello, world!\"", ")" }, result.Tokens);
        Assert.Equal("python-print", result.SelectedOperationId);

        var prerequisiteCandidate = Assert.Single(result.CandidateAssessments
            .Where(candidate => candidate.OperationId == "python-sys-stdout-write"));
        Assert.False(prerequisiteCandidate.IsEligible);
        Assert.Contains("module:sys", prerequisiteCandidate.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Encodes_string_content_using_the_language_pack_literal_rules()
    {
        var loader = new GenerationLanguagePackLoader();
        Assert.True(loader.TryLoadEmbedded(out var packs, out var loadError), loadError);

        var goal = new GenerationGoal(
            "write-standard-output",
            "text",
            "He said \"hello\".\n",
            ImmutableArray<string>.Empty);

        Assert.True(new SequenceGenerator().TryGenerate(
            packs["python"],
            goal,
            out var result,
            out var generationError), generationError);

        Assert.Equal("print(\"He said \\\"hello\\\".\\n\")", result!.Text);
    }

    [Fact]
    public void Rejects_a_semantic_goal_that_no_operation_can_satisfy()
    {
        var loader = new GenerationLanguagePackLoader();
        Assert.True(loader.TryLoadEmbedded(out var packs, out var loadError), loadError);

        var goal = new GenerationGoal(
            "change-object-transform",
            "transform",
            "origin",
            ImmutableArray<string>.Empty);

        Assert.False(new SequenceGenerator().TryGenerate(
            packs["python"],
            goal,
            out var result,
            out var error));

        Assert.Null(result);
        Assert.Contains("No known operation", error, StringComparison.Ordinal);
    }
}
