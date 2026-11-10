using Lattice.Core.Language;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Reasoning;

public sealed class UniversalQuestionTests
{
    private readonly LceParser parser = new();
    private readonly ReasoningEngine engine = new();

    [Fact]
    public void Answers_a_question_from_a_matching_universal_rule()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog is an animal.");

        var result = engine.Ask(knowledgeBase, ParseQuestion("Are all dogs animals?"));

        Assert.Equal(TruthValue.True, result.TruthValue);
        Assert.Equal("Every dog is an animal.", Assert.Single(result.Proofs).SourceText);
    }

    [Fact]
    public void A_broader_rule_supports_a_question_about_a_narrower_class()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog is an animal.");

        var result = engine.Ask(knowledgeBase, ParseQuestion("Are all red dogs animals?"));

        Assert.Equal(TruthValue.True, result.TruthValue);
        Assert.Equal("Every dog is an animal.", Assert.Single(result.Proofs).SourceText);
    }

    [Fact]
    public void A_rule_about_a_narrower_class_does_not_support_a_broader_question()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every red dog is an animal.");

        var result = engine.Ask(knowledgeBase, ParseQuestion("Are all dogs animals?"));

        Assert.Equal(TruthValue.Unknown, result.TruthValue);
    }

    [Fact]
    public void Known_positive_members_do_not_establish_a_universal_claim()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Fido is a dog.");
        Add(knowledgeBase, "Fido is an animal.");

        var result = engine.Ask(knowledgeBase, ParseQuestion("Are all dogs animals?"));

        Assert.Equal(TruthValue.Unknown, result.TruthValue);
    }

    [Fact]
    public void An_explicit_counterexample_refutes_a_universal_question()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Fido is a dog.");
        Add(knowledgeBase, "Fido is not an animal.");

        var result = engine.Ask(knowledgeBase, ParseQuestion("Are all dogs animals?"));

        Assert.Equal(TruthValue.False, result.TruthValue);
        var counterexample = Assert.Single(result.ContradictingProofs);
        Assert.Equal("Fido is not an animal.", counterexample.SourceText);
        Assert.Equal("Fido is a dog.", Assert.Single(counterexample.AdditionalPremises).SourceText);
    }

    [Fact]
    public void A_universal_rule_and_an_explicit_counterexample_are_both_preserved()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog is an animal.");
        Add(knowledgeBase, "Fido is a dog.");
        Add(knowledgeBase, "Fido is not an animal.");

        var result = engine.Ask(knowledgeBase, ParseQuestion("Are all dogs animals?"));

        Assert.Equal(TruthValue.Both, result.TruthValue);
        Assert.Equal("Every dog is an animal.", Assert.Single(result.Proofs).SourceText);
        Assert.Equal("Fido is not an animal.", Assert.Single(result.ContradictingProofs).SourceText);
    }

    [Fact]
    public void A_negative_universal_rule_refutes_the_positive_universal_question()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "No dogs are animals.");

        var result = engine.Ask(knowledgeBase, ParseQuestion("Are all dogs animals?"));

        Assert.Equal(TruthValue.False, result.TruthValue);
        Assert.Equal("No dogs are animals.", Assert.Single(result.ContradictingProofs).SourceText);
    }

    [Fact]
    public void A_counterexample_must_satisfy_every_condition_of_the_queried_class()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Fido is red.");
        Add(knowledgeBase, "Fido is not an animal.");

        var withoutDogMembership = engine.Ask(
            knowledgeBase,
            ParseQuestion("Are all red dogs animals?"));
        Assert.Equal(TruthValue.Unknown, withoutDogMembership.TruthValue);

        Add(knowledgeBase, "Fido is a dog.");

        var withDogMembership = engine.Ask(
            knowledgeBase,
            ParseQuestion("Are all red dogs animals?"));
        Assert.Equal(TruthValue.False, withDogMembership.TruthValue);
    }

    private void Add(KnowledgeBase knowledgeBase, string sentence)
    {
        var parsed = parser.Parse(sentence);
        Assert.True(parsed.IsSuccess, parsed.Error);
        Assert.True(knowledgeBase.TryAdd(parsed.Utterance!, out _, out var error), error);
    }

    private UniversalQuantification ParseQuestion(string sentence)
    {
        var parsed = parser.Parse(sentence);
        Assert.True(parsed.IsSuccess, parsed.Error);
        Assert.Equal(UtteranceKind.Question, parsed.Utterance!.Kind);
        return Assert.IsType<UniversalQuantification>(parsed.Utterance.Meaning);
    }
}
