using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class DefiniteBindingTests
{
    [Theory]
    [InlineData("Write a Python program that set flag to 0 then when flag is equal to 1 then set temporary to 2. Print temporary.", "temporary")]
    [InlineData("Write a Python program that set count to 0 then while count is less than 2 then when count is equal to 1 then set temporary to 2. Print temporary then set count to count + 1.", "temporary")]
    [InlineData("Write a Python program that set text to '' then for each letter in text then print letter. End repetition. Print letter.", "letter")]
    [InlineData("Write a Python program that set text to 'ab' then for each letter in text then set temporary to letter. End repetition. Print temporary.", "temporary")]
    [InlineData("Write a Python program using Pygame. Set flag to 0 then when flag is equal to 1 then invoke api pygame-clock saving result as timer. Invoke api pygame-tick with arguments (timer, 0).", "timer")]
    public void Rejects_values_that_only_a_possibly_skipped_body_introduces(string request, string name)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var error), error);
        Assert.True(SequenceInputFactExtractor.TryExtract(knowledge!, request, out var facts, out error), error);
        Assert.True(new GenerationLanguagePackLoader().TryLoadEmbedded(out var packs, out error), error);
        Assert.False(SemanticProgramPlanner.TryGenerateProgram(facts, packs["python"], out var code, out var statements, out error));
        Assert.Empty(code);
        Assert.Empty(statements);
        Assert.Contains(name, error, StringComparison.Ordinal);
    }

    [Fact]
    public void Both_branches_can_definitely_bind_a_value()
    {
        var program = new SemanticProgram([
            new ConditionalRequirement(new SemanticBooleanExpression(true),
                [new AssignmentRequirement("selected", new SemanticNumberExpression("1"))],
                [new AssignmentRequirement("selected", new SemanticNumberExpression("2"))]),
            new OutputRequirement(new SemanticNameExpression("selected"))]);
        Assert.True(new GenerationLanguagePackLoader().TryLoadEmbedded(out var packs, out var error), error);
        Assert.True(SemanticProgramPlanner.TryPlan(program, packs["python"], out _, out error), error);
    }

    [Fact]
    public void A_dictionary_iteration_does_not_export_uninitialized_item_names()
    {
        var program = new SemanticProgram([
            new AssignmentRequirement("items", new SemanticMapLiteralExpression([])),
            new DictionaryIterationRequirement("key", "value", new SemanticNameExpression("items"),
                [new OutputRequirement(new SemanticNameExpression("key"))]),
            new OutputRequirement(new SemanticNameExpression("value"))]);
        Assert.True(new GenerationLanguagePackLoader().TryLoadEmbedded(out var packs, out var error), error);
        Assert.False(SemanticProgramPlanner.TryPlan(program, packs["python"], out var statements, out error));
        Assert.Empty(statements);
        Assert.Contains("value", error, StringComparison.Ordinal);
    }
}
