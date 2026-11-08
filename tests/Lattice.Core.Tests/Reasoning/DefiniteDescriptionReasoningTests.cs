using Lattice.Core.Language;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Reasoning;

public sealed class DefiniteDescriptionReasoningTests
{
    [Fact]
    public void Stores_and_retrieves_a_relational_fact_with_a_definite_description()
    {
        var parser = new LceParser();
        var knowledgeBase = new KnowledgeBase();
        var engine = new ReasoningEngine();

        Add(knowledgeBase, parser, "Fido likes the red ball.");

        var query = ParseQuestion(parser, "Does Fido like the red ball?");
        var result = engine.Ask(knowledgeBase, query);

        Assert.True(result.IsSupported);
        Assert.Equal("Fido likes the red ball.", result.Proof!.SourceText);
        Assert.Equal("likes(fido, the(ball, red))",
            SemanticFormatter.Format(new SemanticUtterance(
                UtteranceKind.Assertion,
                result.Proof.Predicate,
                result.Proof.SourceText)));
    }

    [Fact]
    public void Does_not_match_a_different_head_or_modifier()
    {
        var parser = new LceParser();
        var knowledgeBase = new KnowledgeBase();
        var engine = new ReasoningEngine();

        Add(knowledgeBase, parser, "Fido likes the red ball.");

        Assert.False(engine.Ask(
            knowledgeBase,
            ParseQuestion(parser, "Does Fido like the blue ball?")).IsSupported);
        Assert.False(engine.Ask(
            knowledgeBase,
            ParseQuestion(parser, "Does Fido like the red car?")).IsSupported);
    }

    [Fact]
    public void Deduplicates_descriptions_independently_of_adjective_order()
    {
        var parser = new LceParser();
        var knowledgeBase = new KnowledgeBase();

        Add(knowledgeBase, parser, "Fido likes the shiny red ball.");
        Add(knowledgeBase, parser, "Fido likes the red shiny ball.");

        Assert.Single(knowledgeBase.Facts);
        Assert.Equal("Fido likes the shiny red ball.", knowledgeBase.Facts[0].SourceText);
    }

    private static void Add(KnowledgeBase knowledgeBase, LceParser parser, string sentence)
    {
        var result = parser.Parse(sentence);
        Assert.True(result.IsSuccess, result.Error);
        Assert.True(
            knowledgeBase.TryAdd(result.Utterance!, out _, out var error),
            error);
    }

    private static PredicateApplication ParseQuestion(LceParser parser, string sentence)
    {
        var result = parser.Parse(sentence);
        Assert.True(result.IsSuccess, result.Error);
        return Assert.IsType<PredicateApplication>(result.Utterance!.Meaning);
    }
}
