using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using Xunit;
using static Lattice.Core.Tests.Reasoning.ReasoningTestSupport;

namespace Lattice.Core.Tests.Reasoning;

public sealed class RuleReasoningTests
{
    private readonly ReasoningEngine engine = new();

    [Fact]
    public void Returns_unknown_when_no_fact_supports_a_rule()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog is an animal.");
        Add(knowledgeBase, "Fido is a cat.");

        var result = engine.Ask(knowledgeBase, ParseQuestion("Is Fido an animal?"));

        Assert.False(result.IsSupported);
        Assert.Null(result.Proof);
    }

    [Fact]
    public void Chains_rules_even_when_they_are_added_in_reverse_order()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every animal is a living-thing.");
        Add(knowledgeBase, "Every dog is an animal.");
        Add(knowledgeBase, "Fido is a dog.");

        var result = engine.Ask(
            knowledgeBase,
            ParseQuestion("Is Fido a living-thing?"));

        Assert.True(result.IsSupported);

        var finalProof = Assert.IsType<ProofNode>(result.Proof);
        Assert.Equal("Every animal is a living-thing.", finalProof.SourceText);

        var middleProof = Assert.IsType<ProofNode>(finalProof.Premise);
        Assert.Equal("Every dog is an animal.", middleProof.SourceText);

        var initialFact = Assert.IsType<ProofNode>(middleProof.Premise);
        Assert.Equal("Fido is a dog.", initialFact.SourceText);
        Assert.Null(initialFact.Premise);
    }

    [Fact]
    public void Answers_a_directly_asserted_fact_without_applying_a_rule()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Fido is a dog.");

        var result = engine.Ask(knowledgeBase, ParseQuestion("Is Fido a dog?"));

        Assert.True(result.IsSupported);
        Assert.Equal("Fido is a dog.", result.Proof!.SourceText);
        Assert.Null(result.Proof.Premise);
    }

    [Fact]
    public void Returns_unknown_for_an_empty_knowledge_base()
    {
        var result = engine.Ask(
            new KnowledgeBase(),
            ParseQuestion("Is Fido a reptile?"));

        Assert.False(result.IsSupported);
    }

    [Fact]
    public void Requires_every_conjunctive_class_condition_for_a_universal_rule()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every red ball is a toy.");
        Add(knowledgeBase, "The red ball is shiny.");
        Add(knowledgeBase, "The blue ball is shiny.");
        Add(knowledgeBase, "The red cube is shiny.");

        var redBall = engine.Ask(
            knowledgeBase,
            ParseQuestion("Is the red ball a toy?"));
        Assert.True(redBall.IsSupported);

        var proof = Assert.IsType<ProofNode>(redBall.Proof);
        Assert.Equal("Every red ball is a toy.", proof.SourceText);
        Assert.Equal("red", proof.Premise!.Predicate.Predicate.Value);
        Assert.Equal("The red ball is shiny.", proof.Premise.SourceText);

        var secondPremise = Assert.Single(proof.AdditionalPremises);
        Assert.Equal("ball", secondPremise.Predicate.Predicate.Value);
        Assert.Equal("The red ball is shiny.", secondPremise.SourceText);

        var blueBall = engine.Ask(
            knowledgeBase,
            ParseQuestion("Is the blue ball a toy?"));
        Assert.False(blueBall.IsSupported);
        Assert.Null(blueBall.Proof);

        var redCube = engine.Ask(
            knowledgeBase,
            ParseQuestion("Is the red cube a toy?"));
        Assert.False(redCube.IsSupported);
        Assert.Null(redCube.Proof);
    }

    [Fact]
    public void Chains_a_conjunctive_rule_into_a_later_unary_rule()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every toy is a plaything.");
        Add(knowledgeBase, "Every red ball is a toy.");
        Add(knowledgeBase, "The red ball is shiny.");

        var result = engine.Ask(
            knowledgeBase,
            ParseQuestion("Is the red ball a plaything?"));

        Assert.True(result.IsSupported);

        var finalProof = Assert.IsType<ProofNode>(result.Proof);
        Assert.Equal("Every toy is a plaything.", finalProof.SourceText);

        var conjunctiveProof = Assert.IsType<ProofNode>(finalProof.Premise);
        Assert.Equal("Every red ball is a toy.", conjunctiveProof.SourceText);
        Assert.Equal("red", conjunctiveProof.Premise!.Predicate.Predicate.Value);
        Assert.Equal("ball", Assert.Single(conjunctiveProof.AdditionalPremises)
            .Predicate.Predicate.Value);
    }
}
