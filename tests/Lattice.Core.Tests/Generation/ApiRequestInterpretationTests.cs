using System.Collections.Immutable;
using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class ApiRequestInterpretationTests
{
    [Fact]
    public void Preserves_imports_and_contract_invocations_in_request_order()
    {
        var program = Interpret("Write a Python program that import math then import pygame then invoke api pygame-init then invoke api pygame-quit.");
        Assert.Collection(program.Requirements,
            requirement => Assert.Equal(new ImportRequirement("math"), requirement),
            requirement => Assert.Equal(new ImportRequirement("pygame"), requirement),
            requirement => Assert.Equal("pygame-init", Assert.IsType<CallRequirement>(requirement).ApiContract),
            requirement => Assert.Equal("pygame-quit", Assert.IsType<CallRequirement>(requirement).ApiContract));
    }

    [Fact]
    public void Rejects_an_undeclared_contract_instead_of_guessing_a_callee()
    {
        var program = Interpret("Write a Python program that invoke api nonexistent-contract.");
        Assert.False(SemanticProgramPlanner.TryPlan(program, Pack(), out var statements, out _));
        Assert.Empty(statements);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Realizes_a_contract_statement_at_supported_depths(int depth)
    {
        ProgramRequirement statement = new CallRequirement("invoke", []) { ApiContract = "pygame-present" };
        for (var index = 0; index < depth; index++)
        {
            statement = new IterationRequirement("item" + index,
                new SemanticListLiteralExpression([new SemanticNumberExpression("1")]), [statement]);
        }
        Assert.True(SemanticProgramPlanner.TryPlan(new SemanticProgram([statement]), Pack(), out var statements, out var error), error);
        Assert.Equal(new string(' ', depth * 4) + "pygame.display.flip()", statements[^1].Text);
    }

    [Fact]
    public void Contract_statement_arguments_are_validated_and_dependency_checked()
    {
        var missing = new CallRequirement("invoke", [new SemanticNameExpression("missing"), new SemanticNumberExpression("60")])
        {
            ApiContract = "pygame-tick"
        };
        Assert.False(SemanticProgramPlanner.TryPlan(new SemanticProgram([missing]), Pack(), out _, out var error));
        Assert.Contains("missing", error, StringComparison.Ordinal);
        var wrongArity = missing with { Arguments = [] };
        Assert.False(SemanticProgramPlanner.TryPlan(new SemanticProgram([wrongArity]), Pack(), out _, out error));
        Assert.Contains("arguments", error, StringComparison.Ordinal);
        var restricted = Pack() with { Operations = Pack().Operations
            .Where(operation => operation.Effect != "emit-expression-statement").ToImmutableArray() };
        Assert.False(SemanticProgramPlanner.TryPlan(new SemanticProgram([
            new CallRequirement("invoke", []) { ApiContract = "pygame-init" }]), restricted, out _, out _));
    }

    private static SemanticProgram Interpret(string request)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var error), error);
        Assert.True(SequenceInputFactExtractor.TryExtractProgram(knowledge!, request, out var program, out error), error);
        return Assert.IsType<SemanticProgram>(program);
    }

    private static GenerationLanguagePack Pack()
    {
        Assert.True(new GenerationLanguagePackLoader().TryLoadEmbedded(out var packs, out var error), error);
        return packs["python"];
    }
}
