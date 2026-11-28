using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class EventRequestInterpretationTests
{
    [Theory]
    [InlineData("pending", "notifications", "closeKind")]
    [InlineData("queued", "queue", "stopCode")]
    public void Captures_event_contracts_results_and_nested_shutdown_state(string wording, string queue, string kind)
    {
        var request = $"Write a Python program using Pygame. Read window close event code saving result as {kind}. " +
            $"Set active to 1 then while active is greater than 0 then read {wording} events saving result as {queue}. " +
            $"For each notification in {queue} then when notification.type is equal to {kind} then set active to 0 then leave repetition. End repetition. End repetition. Print active.";
        var (program, code) = Interpret(request);
        var binding = Assert.IsType<AssignmentRequirement>(program.Requirements[1]);
        Assert.Equal(kind, binding.VariableName);
        Assert.Equal("pygame-quit-event", Assert.IsType<SemanticApiExpression>(binding.Value).Contract);
        var outer = Assert.IsType<WhileRequirement>(program.Requirements[3]);
        binding = Assert.IsType<AssignmentRequirement>(outer.Body[0]);
        Assert.Equal(queue, binding.VariableName);
        Assert.Equal("pygame-events", Assert.IsType<SemanticApiExpression>(binding.Value).Contract);
        var iteration = Assert.IsType<IterationRequirement>(outer.Body[1]);
        var branch = Assert.IsType<ConditionalRequirement>(Assert.Single(iteration.Body));
        Assert.IsType<BreakRequirement>(branch.ThenRequirements[1]);
        Assert.Contains($"{queue} = pygame.event.get()", code, StringComparison.Ordinal);
        Assert.Contains($"{kind} = pygame.QUIT", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Uses_default_bindings_and_preserves_repeated_reads()
    {
        var (_, code) = Interpret("Write a Python program using Pygame. Read window close event code. Read pending events then read queued events.");
        Assert.Contains("close_code = pygame.QUIT", code, StringComparison.Ordinal);
        Assert.Equal(2, code.Split("events = pygame.event.get()", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void Does_not_select_a_library_without_a_declared_import()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var error), error);
        Assert.True(SequenceInputFactExtractor.TryExtract(knowledge!, "Write a Python program. Read queued events. Read window close event code.", out var facts, out error), error);
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
