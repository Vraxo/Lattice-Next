using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class KeyboardRequestInterpretationTests
{
    [Theory]
    [InlineData("keyboard state", "W", "w", "keys", "code")]
    [InlineData("pressed keys", "S", "s", "state", "selected")]
    [InlineData("keyboard state", "up arrow", "up", "inputState", "upCode")]
    [InlineData("pressed keys", "down arrow", "down", "held", "downCode")]
    public void Projects_changed_controls_and_bindings(string wording, string key, string contract, string state, string codeName)
    {
        var request = $"Write a Python program using Pygame. Read {wording} saving result as {state}. Read the {key} key code saving result as {codeName}. When {state}[{codeName}] == True then print 'held'.";
        var (program, code) = Interpret(request);
        var binding = Assert.IsType<AssignmentRequirement>(program.Requirements[1]);
        Assert.Equal(state, binding.VariableName);
        Assert.Equal("pygame-keys", Assert.IsType<SemanticApiExpression>(binding.Value).Contract);
        binding = Assert.IsType<AssignmentRequirement>(program.Requirements[2]);
        Assert.Equal(codeName, binding.VariableName);
        Assert.Equal($"pygame-key-{contract}", Assert.IsType<SemanticApiExpression>(binding.Value).Contract);
        Assert.Contains($"{state} = pygame.key.get_pressed()", code, StringComparison.Ordinal);
        Assert.Contains($"if {state}[{codeName}] == True:", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Supports_default_result_names_and_multiple_controls_in_one_sentence()
    {
        var (_, code) = Interpret("Write a Python program using Pygame. Read keyboard state. Read W key code saving result as first then read S key code saving result as second. When keys[first] == True or keys[second] == True then print 'held'.");
        Assert.Contains("keys = pygame.key.get_pressed()", code, StringComparison.Ordinal);
        Assert.Contains("first = pygame.K_w\nsecond = pygame.K_s", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Does_not_guess_an_undeclared_control()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var error), error);
        Assert.True(SequenceInputFactExtractor.TryExtract(knowledge!, "Write a Python program using Pygame. Read the escape key code.", out var facts, out error), error);
        Assert.DoesNotContain(facts, fact => fact.Id == "program.requirement.api-call");
    }

    private static (SemanticProgram Program, string Code) Interpret(string request)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var error), error);
        Assert.True(SequenceInputFactExtractor.TryExtractProgram(knowledge!, request, out var program, out error), error);
        Assert.True(SequenceInputFactExtractor.TryExtract(knowledge!, request, out var facts, out error), error);
        Assert.True(new GenerationLanguagePackLoader().TryLoadEmbedded(out var packs, out error), error);
        Assert.True(SemanticProgramPlanner.TryGenerateProgram(facts, packs["python"], out var code, out _, out error), error);
        return (Assert.IsType<SemanticProgram>(program), code);
    }
}
