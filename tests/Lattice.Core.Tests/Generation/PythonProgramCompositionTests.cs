using System.Collections.Immutable;
using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class PythonProgramCompositionTests
{
    [Fact]
    public void Composes_assignment_and_expression_output_from_independent_semantic_goals()
    {
        var loader = new GenerationLanguagePackLoader();
        Assert.True(loader.TryLoadEmbedded(out var packs, out var loadError), loadError);

        var goals = ImmutableArray.Create(
            new GenerationGoal(
                "assign-variable",
                "assignment",
                "total = 5",
                ImmutableArray<string>.Empty),
            new GenerationGoal(
                "write-standard-output",
                "expression",
                "total + 3",
                ImmutableArray<string>.Empty));

        var generated = new SequenceGenerationPipeline().TryGenerateProgram(
            packs["python"],
            goals,
            out var result,
            out var error);

        Assert.True(generated, error);
        Assert.Equal(
            $"total = 5{Environment.NewLine}print(total + 3)",
            result!.Text);
        Assert.Equal(
            ["python-assign-variable", "python-print-expression"],
            result.Statements.Select(statement => statement.SelectedOperationId));
    }

    [Fact]
    public void Rejects_a_program_when_any_semantic_step_is_unsupported()
    {
        var loader = new GenerationLanguagePackLoader();
        Assert.True(loader.TryLoadEmbedded(out var packs, out var loadError), loadError);

        var goals = ImmutableArray.Create(
            new GenerationGoal(
                "assign-variable",
                "assignment",
                "total = 5",
                ImmutableArray<string>.Empty),
            new GenerationGoal(
                "delete-files",
                "path",
                "*.tmp",
                ImmutableArray<string>.Empty));

        Assert.False(new SequenceGenerationPipeline().TryGenerateProgram(
            packs["python"],
            goals,
            out var result,
            out var error));

        Assert.Null(result);
        Assert.Contains("No known operation", error, StringComparison.Ordinal);
    }
}
