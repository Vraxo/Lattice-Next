using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using AwesomeAssertions;
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

        result.IsSupported.Should().BeFalse();
        result.Proof.Should().BeNull();
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

        result.IsSupported.Should().BeTrue();

        var finalProof = result.Proof.Should().BeOfType<ProofNode>().Which;
        finalProof.SourceText.Should().Be("Every animal is a living-thing.");

        var middleProof = finalProof.Premise.Should().BeOfType<ProofNode>().Which;
        middleProof.SourceText.Should().Be("Every dog is an animal.");

        var initialFact = middleProof.Premise.Should().BeOfType<ProofNode>().Which;
        initialFact.SourceText.Should().Be("Fido is a dog.");
        initialFact.Premise.Should().BeNull();
    }

    [Fact]
    public void Answers_a_directly_asserted_fact_without_applying_a_rule()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Fido is a dog.");

        var result = engine.Ask(knowledgeBase, ParseQuestion("Is Fido a dog?"));

        result.IsSupported.Should().BeTrue();
        result.Proof!.SourceText.Should().Be("Fido is a dog.");
        result.Proof.Premise.Should().BeNull();
    }

    [Fact]
    public void Returns_unknown_for_an_empty_knowledge_base()
    {
        var result = engine.Ask(
            new KnowledgeBase(),
            ParseQuestion("Is Fido a reptile?"));

        result.IsSupported.Should().BeFalse();
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
        redBall.IsSupported.Should().BeTrue();

        var proof = redBall.Proof.Should().BeOfType<ProofNode>().Which;
        proof.SourceText.Should().Be("Every red ball is a toy.");
        proof.Premise!.Predicate.Predicate.Value.Should().Be("red");
        proof.Premise.SourceText.Should().Be("The red ball is shiny.");

        var secondPremise = proof.AdditionalPremises.Should().ContainSingle().Which;
        secondPremise.Predicate.Predicate.Value.Should().Be("ball");
        secondPremise.SourceText.Should().Be("The red ball is shiny.");

        var blueBall = engine.Ask(
            knowledgeBase,
            ParseQuestion("Is the blue ball a toy?"));
        blueBall.IsSupported.Should().BeFalse();
        blueBall.Proof.Should().BeNull();

        var redCube = engine.Ask(
            knowledgeBase,
            ParseQuestion("Is the red cube a toy?"));
        redCube.IsSupported.Should().BeFalse();
        redCube.Proof.Should().BeNull();
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

        result.IsSupported.Should().BeTrue();

        var finalProof = result.Proof.Should().BeOfType<ProofNode>().Which;
        finalProof.SourceText.Should().Be("Every toy is a plaything.");

        var conjunctiveProof = finalProof.Premise.Should().BeOfType<ProofNode>().Which;
        conjunctiveProof.SourceText.Should().Be("Every red ball is a toy.");
        conjunctiveProof.Premise!.Predicate.Predicate.Value.Should().Be("red");
        conjunctiveProof.AdditionalPremises.Should().ContainSingle().Which
            .Predicate.Predicate.Value.Should().Be("ball");
    }
}
