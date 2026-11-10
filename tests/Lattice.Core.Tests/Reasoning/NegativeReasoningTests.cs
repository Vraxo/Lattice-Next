using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using AwesomeAssertions;
using Xunit;
using static Lattice.Core.Tests.Reasoning.ReasoningTestSupport;

namespace Lattice.Core.Tests.Reasoning;

public sealed class NegativeReasoningTests
{
    private readonly ReasoningEngine engine = new();

    [Fact]
    public void Distinguishes_true_false_both_and_unknown_evidence()
    {
        var knowledgeBase = new KnowledgeBase();

        Add(knowledgeBase, "Fido is a dog.");
        var supported = engine.Ask(knowledgeBase, ParseQuestion("Is Fido a dog?"));
        supported.TruthValue.Should().Be(TruthValue.True);

        Add(knowledgeBase, "Fido is not a dog.");
        var contradictory = engine.Ask(knowledgeBase, ParseQuestion("Is Fido a dog?"));
        contradictory.TruthValue.Should().Be(TruthValue.Both);
        contradictory.Proofs.Should().ContainSingle().Which;
        contradictory.ContradictingProofs.Should().ContainSingle().Which;
        contradictory.Proof!.Polarity.Should().Be(EvidencePolarity.Positive);
        contradictory.ContradictingProofs[0].Polarity.Should().Be(EvidencePolarity.Negative);

        var unknown = engine.Ask(knowledgeBase, ParseQuestion("Is Rex a dog?"));
        unknown.TruthValue.Should().Be(TruthValue.Unknown);

        var negativeOnlyKnowledge = new KnowledgeBase();
        Add(negativeOnlyKnowledge, "Fido is not a dog.");
        var explicitlyFalse = engine.Ask(
            negativeOnlyKnowledge,
            ParseQuestion("Is Fido a dog?"));
        explicitlyFalse.TruthValue.Should().Be(TruthValue.False);
        explicitlyFalse.Proof.Should().BeNull();
        explicitlyFalse.ContradictingProofs.Should().ContainSingle().Which;
    }

    [Fact]
    public void Answers_a_negated_question_from_explicit_negative_evidence()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Fido is not a dog.");

        var result = engine.Ask(
            knowledgeBase,
            ParseNegatedQuestion("Is Fido not a dog?"));

        result.TruthValue.Should().Be(TruthValue.True);
        result.Proof!.Polarity.Should().Be(EvidencePolarity.Negative);
        result.Proof.SourceText.Should().Be("Fido is not a dog.");
    }

    [Fact]
    public void Preserves_both_sides_when_asking_a_negated_question()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Fido is a dog.");
        Add(knowledgeBase, "Fido is not a dog.");

        var result = engine.Ask(
            knowledgeBase,
            ParseNegatedQuestion("Is Fido not a dog?"));

        result.TruthValue.Should().Be(TruthValue.Both);
        result.Proof!.Polarity.Should().Be(EvidencePolarity.Negative);
        result.ContradictingProofs.Should().ContainSingle().Which.Polarity.Should().Be(EvidencePolarity.Positive);
    }

    [Fact]
    public void Negative_facts_do_not_satisfy_positive_rule_premises()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog is an animal.");
        Add(knowledgeBase, "Fido is not a dog.");

        var result = engine.Ask(
            knowledgeBase,
            ParseQuestion("Is Fido an animal?"));

        result.TruthValue.Should().Be(TruthValue.Unknown);
        result.ContradictingProofs.Should().BeEmpty();
    }

    [Fact]
    public void Contradictory_evidence_does_not_cancel_a_positive_derivation()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog is an animal.");
        Add(knowledgeBase, "Fido is a dog.");
        Add(knowledgeBase, "Fido is not a dog.");

        var result = engine.Ask(
            knowledgeBase,
            ParseQuestion("Is Fido an animal?"));

        result.TruthValue.Should().Be(TruthValue.True);
        result.Proof!.SourceText.Should().Be("Every dog is an animal.");
    }

    [Fact]
    public void A_contradiction_does_not_trigger_unrelated_positive_rules()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog is an animal.");
        Add(knowledgeBase, "Every reptile is happy.");
        Add(knowledgeBase, "Fido is a dog.");
        Add(knowledgeBase, "Fido is not a dog.");

        var unrelated = engine.Ask(
            knowledgeBase,
            ParseQuestion("Is Fido happy?"));

        unrelated.TruthValue.Should().Be(TruthValue.Unknown);
    }

    [Fact]
    public void Stores_positive_and_negative_evidence_as_separate_facts()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Fido is a dog.");
        Add(knowledgeBase, "Fido is not a dog.");

        knowledgeBase.Facts.Count.Should().Be(2);
        knowledgeBase.Facts.Should().Contain(fact =>
            fact.Polarity == EvidencePolarity.Positive);
        knowledgeBase.Facts.Should().Contain(fact =>
            fact.Polarity == EvidencePolarity.Negative);
    }

    [Fact]
    public void Uses_a_derived_negative_conclusion_as_a_premise_for_a_later_positive_rule()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog is not a cat.");
        Add(knowledgeBase, "Every dog that is not a cat is friendly.");
        Add(knowledgeBase, "Fido is a dog.");

        var result = engine.Ask(knowledgeBase, ParseQuestion("Is Fido friendly?"));

        result.TruthValue.Should().Be(TruthValue.True);
        result.Proof!.SourceText.Should().Be("Every dog that is not a cat is friendly.");

        var negativePremise = result.Proof.AdditionalPremises.Should().ContainSingle().Which;
        negativePremise.SourceText.Should().Be("Every dog is not a cat.");
        negativePremise.Polarity.Should().Be(EvidencePolarity.Negative);
        negativePremise.Premise!.SourceText.Should().Be("Fido is a dog.");
    }

    [Fact]
    public void Uses_a_derived_negative_conclusion_as_a_premise_for_a_later_negative_rule()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog is not a cat.");
        Add(knowledgeBase, "Every dog that is not a cat is not a wolf.");
        Add(knowledgeBase, "Fido is a dog.");

        var result = engine.Ask(knowledgeBase, ParseQuestion("Is Fido a wolf?"));

        result.TruthValue.Should().Be(TruthValue.False);
        var negativeWolf = result.ContradictingProofs.Should().ContainSingle().Which;
        negativeWolf.SourceText.Should().Be("Every dog that is not a cat is not a wolf.");

        var negativeCat = negativeWolf.AdditionalPremises.Should().ContainSingle().Which;
        negativeCat.SourceText.Should().Be("Every dog is not a cat.");
        negativeCat.Polarity.Should().Be(EvidencePolarity.Negative);
        negativeCat.Premise!.SourceText.Should().Be("Fido is a dog.");
    }

    [Fact]
    public void Does_not_create_circular_proofs_when_negative_rules_form_a_cycle()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog is not a cat.");
        Add(knowledgeBase, "Every dog that is not a cat is not a wolf.");
        Add(knowledgeBase, "Every dog that is not a wolf is not a cat.");
        Add(knowledgeBase, "Fido is a dog.");

        var result = engine.Ask(knowledgeBase, ParseQuestion("Is Fido a cat?"));

        result.TruthValue.Should().Be(TruthValue.False);
        var negativeCat = result.ContradictingProofs.Should().ContainSingle().Which;
        negativeCat.SourceText.Should().Be("Every dog is not a cat.");
        negativeCat.Premise!.SourceText.Should().Be("Fido is a dog.");
    }

    [Fact]
    public void Retains_positive_and_negative_rule_conclusions_as_both()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog is not a cat.");
        Add(knowledgeBase, "Fido is a dog.");
        Add(knowledgeBase, "Fido is a cat.");

        var result = engine.Ask(knowledgeBase, ParseQuestion("Is Fido a cat?"));

        result.TruthValue.Should().Be(TruthValue.Both);
        result.Proof!.SourceText.Should().Be("Fido is a cat.");
        result.ContradictingProofs.Should().ContainSingle().Which.SourceText.Should().Be("Every dog is not a cat.");
    }

    [Fact]
    public void Applies_a_rule_when_an_explicit_negative_fact_satisfies_a_premise()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog that is not a cat is friendly.");
        Add(knowledgeBase, "Fido is a dog.");
        Add(knowledgeBase, "Fido is not a cat.");

        var rule = knowledgeBase.Rules.Should().ContainSingle().Which;
        rule.Premises.Should().ContainSingle().Which.Predicate.Value.Should().Be("dog");
        rule.NegativePremises.Should().ContainSingle().Which.Predicate.Value.Should().Be("cat");

        var result = engine.Ask(knowledgeBase, ParseQuestion("Is Fido friendly?"));

        result.TruthValue.Should().Be(TruthValue.True);
        result.Proof!.SourceText.Should().Be("Every dog that is not a cat is friendly.");
        result.Proof.Premise!.SourceText.Should().Be("Fido is a dog.");
        var negativePremise = result.Proof.AdditionalPremises.Should().ContainSingle().Which;
        negativePremise.SourceText.Should().Be("Fido is not a cat.");
        negativePremise.Polarity.Should().Be(EvidencePolarity.Negative);
    }

    [Fact]
    public void Does_not_apply_a_negated_premise_without_explicit_negative_evidence()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog that is not a cat is friendly.");
        Add(knowledgeBase, "Fido is a dog.");

        var unknown = engine.Ask(knowledgeBase, ParseQuestion("Is Fido friendly?"));
        unknown.TruthValue.Should().Be(TruthValue.Unknown);

        Add(knowledgeBase, "Fido is a cat.");

        var positiveOnly = engine.Ask(knowledgeBase, ParseQuestion("Is Fido friendly?"));
        positiveOnly.TruthValue.Should().Be(TruthValue.Unknown);
    }

    [Fact]
    public void Both_evidence_still_satisfies_a_negated_premise_without_discarding_the_contradiction()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog that is not a cat is friendly.");
        Add(knowledgeBase, "Fido is a dog.");
        Add(knowledgeBase, "Fido is a cat.");
        Add(knowledgeBase, "Fido is not a cat.");

        var result = engine.Ask(knowledgeBase, ParseQuestion("Is Fido friendly?"));

        result.TruthValue.Should().Be(TruthValue.True);
        result.Proof!.AdditionalPremises.Should().ContainSingle().Which.SourceText.Should().Be("Fido is not a cat.");
        knowledgeBase.Facts.Should().Contain(fact =>
            fact.Predicate.Predicate.Value == "cat" &&
            fact.Polarity == EvidencePolarity.Positive);
        knowledgeBase.Facts.Should().Contain(fact =>
            fact.Predicate.Predicate.Value == "cat" &&
            fact.Polarity == EvidencePolarity.Negative);
    }

    [Fact]
    public void Does_not_use_derived_negative_evidence_to_satisfy_positive_premises()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog is not a cat.");
        Add(knowledgeBase, "Every cat is an animal.");
        Add(knowledgeBase, "Fido is a dog.");

        var negativeCat = engine.Ask(knowledgeBase, ParseQuestion("Is Fido a cat?"));
        var animal = engine.Ask(knowledgeBase, ParseQuestion("Is Fido an animal?"));

        negativeCat.TruthValue.Should().Be(TruthValue.False);
        animal.TruthValue.Should().Be(TruthValue.Unknown);
    }
}
