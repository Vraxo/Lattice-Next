using System.Collections.Immutable;
using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class ConditionalBlockInterpretationTests
{
    [Theory]
    [InlineData("When")]
    [InlineData("If")]
    public void Later_sentences_are_sibling_requirements_not_part_of_a_branch(string word)
    {
        var program = Interpret($"Write a Python program. Set amount to 2. {word} amount is greater than 0 then print 'positive'. " +
            $"{word} amount is less than 0 then print 'negative'. Set amount to 7 then print amount.");
        Assert.Equal(5, program.Requirements.Length);
        Assert.IsType<AssignmentRequirement>(program.Requirements[0]);
        var first = Assert.IsType<ConditionalRequirement>(program.Requirements[1]);
        var second = Assert.IsType<ConditionalRequirement>(program.Requirements[2]);
        Assert.Equal(new OutputRequirement(new SemanticTextExpression("positive")), Assert.Single(first.ThenRequirements));
        Assert.Equal(new OutputRequirement(new SemanticTextExpression("negative")), Assert.Single(second.ThenRequirements));
        Assert.IsType<AssignmentRequirement>(program.Requirements[3]);
        Assert.Equal(new OutputRequirement(new SemanticNameExpression("amount")), program.Requirements[4]);
    }

    [Fact]
    public void Builds_inner_conditions_before_enclosing_conditions()
    {
        var program = Interpret("Write a Python program that set amount to 2 then when amount is greater than 0 " +
            "then when amount is less than 5 then print 'inside'.");
        var outer = Assert.IsType<ConditionalRequirement>(program.Requirements[1]);
        var inner = Assert.IsType<ConditionalRequirement>(Assert.Single(outer.ThenRequirements));
        Assert.Equal(new OutputRequirement(new SemanticTextExpression("inside")), Assert.Single(inner.ThenRequirements));
    }

    [Fact]
    public void Keeps_independent_conditions_in_a_continuous_loop_body()
    {
        var program = Interpret("Write a Python program that set count to 0 then while count is less than 4 then set count to count + 1. " +
            "When count is equal to 2 then print 'two'. When count is equal to 4 then print 'four'.");
        var loop = Assert.IsType<WhileRequirement>(program.Requirements[1]);
        Assert.Collection(loop.Body,
            statement => Assert.IsType<AssignmentRequirement>(statement),
            statement => Assert.IsType<ConditionalRequirement>(statement),
            statement => Assert.IsType<ConditionalRequirement>(statement));
    }

    [Fact]
    public void Separates_the_single_declared_else_from_a_later_sentence()
    {
        var program = Interpret("Write a Python program that set count to 0 then when count is greater than 0 then print 'yes' otherwise print 'no'. " +
            "Set count to 1 then print count.");
        var branch = Assert.IsType<ConditionalRequirement>(program.Requirements[1]);
        Assert.Equal(new OutputRequirement(new SemanticTextExpression("yes")), Assert.Single(branch.ThenRequirements));
        Assert.Equal(new OutputRequirement(new SemanticTextExpression("no")), Assert.Single(branch.ElseRequirements));
        Assert.Equal(4, program.Requirements.Length);
    }

    [Fact]
    public void Sentence_metadata_ignores_quoted_and_embedded_periods()
    {
        const string request = "Write a Python program that set amount to 0 then when amount is less than 0.5 then print 'a.b'. Set amount to 2 then print amount.";
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var error), error);
        Assert.True(SequenceInputFactExtractor.TryExtract(knowledge!, request, out var facts, out error), error);
        var condition = Assert.Single(facts, fact => fact.Id == "program.requirement.condition");
        var output = Assert.Single(facts, fact => fact.Id == "program.requirement.output");
        var end = Assert.Single(condition.CollectionSentenceEnds["condition"]);
        Assert.True(end > output.CollectionPositions["value"][0]);
        Assert.True(end < output.CollectionPositions["value"][1]);
        Assert.True(SemanticProgramFactMapper.TryCreate(facts, out var program, out error), error);
        Assert.Equal(4, program!.Requirements.Length);
        var invalid = facts.Select(fact => fact.Id == condition.Id
            ? fact with { CollectionSentenceEnds = ImmutableDictionary<string, ImmutableArray<int>>.Empty.Add("condition", []) }
            : fact).ToImmutableArray();
        Assert.False(SemanticProgramFactMapper.TryCreate(invalid, out _, out _));
    }

    [Theory]
    [InlineData("Write a Python program that set count to 0 then when count is greater than 0 then. Set count to 1 then print count.")]
    [InlineData("Write a Python program that set count to 0 then when count is greater than 0 then print count otherwise. Set count to 1 then print count.")]
    public void Does_not_fill_an_empty_branch_with_statements_from_a_later_sentence(string request)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var error), error);
        Assert.False(SequenceInputFactExtractor.TryExtractProgram(knowledge!, request, out _, out _));
    }

    private static SemanticProgram Interpret(string request)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var error), error);
        Assert.True(SequenceInputFactExtractor.TryExtractProgram(knowledge!, request, out var program, out error), error);
        return Assert.IsType<SemanticProgram>(program);
    }
}
