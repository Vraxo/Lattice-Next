using System.Collections.Immutable;
using Lattice.Core.Language;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using Xunit;

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
        Assert.Equal(TruthValue.True, supported.TruthValue);

        Add(knowledgeBase, "Fido is not a dog.");
        var contradictory = engine.Ask(knowledgeBase, ParseQuestion("Is Fido a dog?"));
        Assert.Equal(TruthValue.Both, contradictory.TruthValue);
        Assert.Single(contradictory.Proofs);
        Assert.Single(contradictory.ContradictingProofs);
        Assert.Equal(EvidencePolarity.Positive, contradictory.Proof!.Polarity);
        Assert.Equal(EvidencePolarity.Negative, contradictory.ContradictingProofs[0].Polarity);

        var unknown = engine.Ask(knowledgeBase, ParseQuestion("Is Rex a dog?"));
        Assert.Equal(TruthValue.Unknown, unknown.TruthValue);

        var negativeOnlyKnowledge = new KnowledgeBase();
        Add(negativeOnlyKnowledge, "Fido is not a dog.");
        var explicitlyFalse = engine.Ask(
            negativeOnlyKnowledge,
            ParseQuestion("Is Fido a dog?"));
        Assert.Equal(TruthValue.False, explicitlyFalse.TruthValue);
        Assert.Null(explicitlyFalse.Proof);
        Assert.Single(explicitlyFalse.ContradictingProofs);
    }

    [Fact]
    public void Answers_a_negated_question_from_explicit_negative_evidence()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Fido is not a dog.");

        var result = engine.Ask(
            knowledgeBase,
            ParseNegatedQuestion("Is Fido not a dog?"));

        Assert.Equal(TruthValue.True, result.TruthValue);
        Assert.Equal(EvidencePolarity.Negative, result.Proof!.Polarity);
        Assert.Equal("Fido is not a dog.", result.Proof.SourceText);
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

        Assert.Equal(TruthValue.Both, result.TruthValue);
        Assert.Equal(EvidencePolarity.Negative, result.Proof!.Polarity);
        Assert.Equal(EvidencePolarity.Positive, Assert.Single(result.ContradictingProofs).Polarity);
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

        Assert.Equal(TruthValue.Unknown, result.TruthValue);
        Assert.Empty(result.ContradictingProofs);
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

        Assert.Equal(TruthValue.True, result.TruthValue);
        Assert.Equal("Every dog is an animal.", result.Proof!.SourceText);
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

        Assert.Equal(TruthValue.Unknown, unrelated.TruthValue);
    }

    [Fact]
    public void Stores_positive_and_negative_evidence_as_separate_facts()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Fido is a dog.");
        Add(knowledgeBase, "Fido is not a dog.");

        Assert.Equal(2, knowledgeBase.Facts.Count);
        Assert.Contains(knowledgeBase.Facts, fact =>
            fact.Polarity == EvidencePolarity.Positive);
        Assert.Contains(knowledgeBase.Facts, fact =>
            fact.Polarity == EvidencePolarity.Negative);
    }

    [Fact]
    public void Negative_relational_evidence_answers_exact_queries_without_assuming_unrelated_relations()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Fido does not like milk.");

        var positiveQuery = engine.Ask(
            knowledgeBase,
            ParseQuestion("Does Fido like milk?"));
        var negativeQuery = engine.Ask(
            knowledgeBase,
            ParseNegatedQuestion("Does Fido not like milk?"));
        var differentObject = engine.Ask(
            knowledgeBase,
            ParseQuestion("Does Fido like water?"));

        Assert.Equal(TruthValue.False, positiveQuery.TruthValue);
        Assert.Null(positiveQuery.Proof);
        Assert.Equal("Fido does not like milk.", Assert.Single(positiveQuery.ContradictingProofs).SourceText);
        Assert.Equal(TruthValue.True, negativeQuery.TruthValue);
        Assert.Equal(EvidencePolarity.Negative, negativeQuery.Proof!.Polarity);
        Assert.Equal(TruthValue.Unknown, differentObject.TruthValue);
    }

    [Fact]
    public void Positive_and_negative_relational_evidence_remain_independent()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Fido likes milk.");
        Add(knowledgeBase, "Fido does not like milk.");

        var result = engine.Ask(
            knowledgeBase,
            ParseQuestion("Does Fido like milk?"));

        Assert.Equal(TruthValue.Both, result.TruthValue);
        Assert.Equal("Fido likes milk.", result.Proof!.SourceText);
        Assert.Equal("Fido does not like milk.", Assert.Single(result.ContradictingProofs).SourceText);
        Assert.Equal(2, knowledgeBase.Facts.Count);
    }

    [Fact]
    public void Uses_a_derived_negative_conclusion_as_a_premise_for_a_later_positive_rule()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog is not a cat.");
        Add(knowledgeBase, "Every dog that is not a cat is friendly.");
        Add(knowledgeBase, "Fido is a dog.");

        var result = engine.Ask(knowledgeBase, ParseQuestion("Is Fido friendly?"));

        Assert.Equal(TruthValue.True, result.TruthValue);
        Assert.Equal(
            "Every dog that is not a cat is friendly.",
            result.Proof!.SourceText);

        var negativePremise = Assert.Single(result.Proof.AdditionalPremises);
        Assert.Equal("Every dog is not a cat.", negativePremise.SourceText);
        Assert.Equal(EvidencePolarity.Negative, negativePremise.Polarity);
        Assert.Equal("Fido is a dog.", negativePremise.Premise!.SourceText);
    }

    [Fact]
    public void Uses_a_derived_negative_conclusion_as_a_premise_for_a_later_negative_rule()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog is not a cat.");
        Add(knowledgeBase, "Every dog that is not a cat is not a wolf.");
        Add(knowledgeBase, "Fido is a dog.");

        var result = engine.Ask(knowledgeBase, ParseQuestion("Is Fido a wolf?"));

        Assert.Equal(TruthValue.False, result.TruthValue);
        var negativeWolf = Assert.Single(result.ContradictingProofs);
        Assert.Equal(
            "Every dog that is not a cat is not a wolf.",
            negativeWolf.SourceText);

        var negativeCat = Assert.Single(negativeWolf.AdditionalPremises);
        Assert.Equal("Every dog is not a cat.", negativeCat.SourceText);
        Assert.Equal(EvidencePolarity.Negative, negativeCat.Polarity);
        Assert.Equal("Fido is a dog.", negativeCat.Premise!.SourceText);
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

        Assert.Equal(TruthValue.False, result.TruthValue);
        var negativeCat = Assert.Single(result.ContradictingProofs);
        Assert.Equal("Every dog is not a cat.", negativeCat.SourceText);
        Assert.Equal("Fido is a dog.", negativeCat.Premise!.SourceText);
    }

    [Fact]
    public void Uses_a_derived_negative_premise_for_a_fixed_object_relational_conclusion()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog is not a cat.");
        Add(knowledgeBase, "Every dog that is not a cat does not like milk.");
        Add(knowledgeBase, "Fido is a dog.");

        var result = engine.Ask(knowledgeBase, ParseQuestion("Does Fido like milk?"));

        Assert.Equal(TruthValue.False, result.TruthValue);
        var negativeRelation = Assert.Single(result.ContradictingProofs);
        Assert.Equal(
            "Every dog that is not a cat does not like milk.",
            negativeRelation.SourceText);

        var negativePremise = Assert.Single(negativeRelation.AdditionalPremises);
        Assert.Equal("Every dog is not a cat.", negativePremise.SourceText);
        Assert.Equal(EvidencePolarity.Negative, negativePremise.Polarity);
        Assert.Equal("Fido is a dog.", negativePremise.Premise!.SourceText);
    }

    [Fact]
    public void Retains_positive_and_negative_rule_conclusions_as_both()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog is not a cat.");
        Add(knowledgeBase, "Fido is a dog.");
        Add(knowledgeBase, "Fido is a cat.");

        var result = engine.Ask(knowledgeBase, ParseQuestion("Is Fido a cat?"));

        Assert.Equal(TruthValue.Both, result.TruthValue);
        Assert.Equal("Fido is a cat.", result.Proof!.SourceText);
        Assert.Equal("Every dog is not a cat.", Assert.Single(result.ContradictingProofs).SourceText);
    }

    [Fact]
    public void Derives_negative_fixed_object_relations_from_positive_unary_premises()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog does not like milk.");
        Add(knowledgeBase, "Fido is a dog.");

        var result = engine.Ask(knowledgeBase, ParseQuestion("Does Fido like milk?"));

        Assert.Equal(TruthValue.False, result.TruthValue);
        Assert.Equal("Every dog does not like milk.", Assert.Single(result.ContradictingProofs).SourceText);
    }

    [Fact]
    public void Applies_a_rule_when_an_explicit_negative_fact_satisfies_a_premise()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog that is not a cat is friendly.");
        Add(knowledgeBase, "Fido is a dog.");
        Add(knowledgeBase, "Fido is not a cat.");

        var rule = Assert.Single(knowledgeBase.Rules);
        Assert.Equal("dog", Assert.Single(rule.Premises).Predicate.Value);
        Assert.Equal("cat", Assert.Single(rule.NegativePremises).Predicate.Value);

        var result = engine.Ask(knowledgeBase, ParseQuestion("Is Fido friendly?"));

        Assert.Equal(TruthValue.True, result.TruthValue);
        Assert.Equal("Every dog that is not a cat is friendly.", result.Proof!.SourceText);
        Assert.Equal("Fido is a dog.", result.Proof.Premise!.SourceText);
        var negativePremise = Assert.Single(result.Proof.AdditionalPremises);
        Assert.Equal("Fido is not a cat.", negativePremise.SourceText);
        Assert.Equal(EvidencePolarity.Negative, negativePremise.Polarity);
    }

    [Fact]
    public void Does_not_apply_a_negated_premise_without_explicit_negative_evidence()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog that is not a cat is friendly.");
        Add(knowledgeBase, "Fido is a dog.");

        var unknown = engine.Ask(knowledgeBase, ParseQuestion("Is Fido friendly?"));
        Assert.Equal(TruthValue.Unknown, unknown.TruthValue);

        Add(knowledgeBase, "Fido is a cat.");

        var positiveOnly = engine.Ask(knowledgeBase, ParseQuestion("Is Fido friendly?"));
        Assert.Equal(TruthValue.Unknown, positiveOnly.TruthValue);
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

        Assert.Equal(TruthValue.True, result.TruthValue);
        Assert.Equal("Fido is not a cat.", Assert.Single(result.Proof!.AdditionalPremises).SourceText);
        Assert.Contains(knowledgeBase.Facts, fact =>
            fact.Predicate.Predicate.Value == "cat" &&
            fact.Polarity == EvidencePolarity.Positive);
        Assert.Contains(knowledgeBase.Facts, fact =>
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

        Assert.Equal(TruthValue.False, negativeCat.TruthValue);
        Assert.Equal(TruthValue.Unknown, animal.TruthValue);
    }

    [Fact]
    public void Applies_negated_premises_to_fixed_object_relational_conclusions()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog that is not a cat likes milk.");
        Add(knowledgeBase, "Fido is a dog.");
        Add(knowledgeBase, "Fido is not a cat.");

        var result = engine.Ask(knowledgeBase, ParseQuestion("Does Fido like milk?"));

        Assert.Equal(TruthValue.True, result.TruthValue);
        Assert.Equal("Every dog that is not a cat likes milk.", result.Proof!.SourceText);
        Assert.Equal("Fido is not a cat.", result.Proof.AdditionalPremises.Single().SourceText);
    }

    private static ExistentialQuantification ParseExistentialQuestion(string sentence)
    {
        var utterance = Parse(sentence);
        Assert.Equal(UtteranceKind.Question, utterance.Kind);
        return Assert.IsType<ExistentialQuantification>(utterance.Meaning);
    }

    private static KnowledgeEntryKind Add(
        KnowledgeBase knowledgeBase,
        string sentence)
    {
        var accepted = knowledgeBase.TryAdd(
            Parse(sentence),
            out var kind,
            out var error);

        Assert.True(accepted, error);
        return kind;
    }

    private static Negation ParseNegatedQuestion(string sentence)
    {
        var utterance = Parse(sentence);
        Assert.Equal(UtteranceKind.Question, utterance.Kind);
        return Assert.IsType<Negation>(utterance.Meaning);
    }

    private static PredicateApplication ParseQuestion(string sentence)
    {
        var utterance = Parse(sentence);
        Assert.Equal(UtteranceKind.Question, utterance.Kind);
        return Assert.IsType<PredicateApplication>(utterance.Meaning);
    }

    private static SemanticUtterance Parse(string sentence)
    {
        var result = new LceParser().Parse(sentence);
        Assert.True(result.IsSuccess, result.Error);
        return Assert.IsType<SemanticUtterance>(result.Utterance);
    }
}


    private static Negation ParseNegatedQuestion(string sentence)
    {
        var utterance = Parse(sentence);
        Assert.Equal(UtteranceKind.Question, utterance.Kind);
        return Assert.IsType<Negation>(utterance.Meaning);
    }

    private static KnowledgeEntryKind Add(KnowledgeBase knowledgeBase, string sentence)
    {
        var accepted = knowledgeBase.TryAdd(Parse(sentence), out var kind, out var error);
        Assert.True(accepted, error);
        return kind;
    }

    private static PredicateApplication ParseQuestion(string sentence)
    {
        var utterance = Parse(sentence);
        Assert.Equal(UtteranceKind.Question, utterance.Kind);
        return Assert.IsType<PredicateApplication>(utterance.Meaning);
    }

    private static SemanticUtterance Parse(string sentence)
    {
        var result = new LceParser().Parse(sentence);
        Assert.True(result.IsSuccess, result.Error);
        return Assert.IsType<SemanticUtterance>(result.Utterance);
    }
}
