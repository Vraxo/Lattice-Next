using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using Xunit;
using static Lattice.Core.Tests.Reasoning.ReasoningTestSupport;

namespace Lattice.Core.Tests.Reasoning;

public sealed class RelationalReasoningTests
{
    private readonly ReasoningEngine engine = new();

    [Fact]
    public void Uses_an_explicit_fixed_object_relation_as_a_universal_rule_premise()
    {
        var kb = new KnowledgeBase();
        Add(kb, "Every dog that likes milk is friendly.");
        Add(kb, "Fido is a dog.");
        Add(kb, "Fido likes milk.");
        var result = engine.Ask(kb, ParseQuestion("Is Fido friendly?"));
        Assert.Equal(TruthValue.True, result.TruthValue);
        Assert.Equal("Every dog that likes milk is friendly.", result.Proof!.SourceText);
        Assert.Equal("Fido is a dog.", result.Proof.Premise!.SourceText);
        var relation = Assert.Single(result.Proof.AdditionalPremises);
        Assert.Equal("likes", relation.Predicate.Predicate.Value);
        Assert.Equal("Fido likes milk.", relation.SourceText);
    }

    [Fact]
    public void Missing_or_negative_relations_do_not_satisfy_positive_rule_premises()
    {
        var kb = new KnowledgeBase();
        Add(kb, "Every dog that likes milk is friendly.");
        Add(kb, "Fido is a dog.");
        Assert.Equal(TruthValue.Unknown, engine.Ask(kb, ParseQuestion("Is Fido friendly?")).TruthValue);
        Add(kb, "Fido does not like milk.");
        Assert.Equal(TruthValue.Unknown, engine.Ask(kb, ParseQuestion("Is Fido friendly?")).TruthValue);
    }

    [Fact]
    public void Relational_premises_must_match_the_same_individual()
    {
        var kb = new KnowledgeBase();
        Add(kb, "Every dog that likes milk is friendly.");
        Add(kb, "Fido is a dog.");
        Add(kb, "Rex likes milk.");
        Assert.Equal(TruthValue.Unknown, engine.Ask(kb, ParseQuestion("Is Fido friendly?")).TruthValue);
    }

    [Fact]
    public void Infers_a_relational_conclusion_from_a_matching_unary_fact()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog likes milk.");
        Add(knowledgeBase, "Fido is a dog.");

        var result = engine.Ask(
            knowledgeBase,
            ParseQuestion("Does Fido like milk?"));

        Assert.True(result.IsSupported);
        var proof = Assert.IsType<ProofNode>(result.Proof);
        Assert.Equal("likes", proof.Predicate.Predicate.Value);
        Assert.Equal("Every dog likes milk.", proof.SourceText);
        Assert.Equal(ProofNodeKind.Derived, proof.Kind);
        Assert.Equal("fido", Assert.IsType<IndividualTerm>(proof.Predicate.Arguments[0]).Identifier.Value);
        Assert.Equal("milk", Assert.IsType<IndividualTerm>(proof.Predicate.Arguments[1]).Identifier.Value);

        var premise = Assert.IsType<ProofNode>(proof.Premise);
        Assert.Equal("Fido is a dog.", premise.SourceText);
        Assert.Equal("dog", premise.Predicate.Predicate.Value);
        Assert.Equal(ProofNodeKind.Asserted, premise.Kind);
    }

    [Fact]
    public void Applies_conjunctive_relational_rule_conditions_to_the_same_individual()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every red dog likes milk.");
        Add(knowledgeBase, "The red dog is shiny.");
        Add(knowledgeBase, "The blue dog is shiny.");

        var supported = engine.Ask(
            knowledgeBase,
            ParseQuestion("Does the red dog like milk?"));

        Assert.True(supported.IsSupported);
        Assert.Equal("Every red dog likes milk.", supported.Proof!.SourceText);
        Assert.Equal("red", supported.Proof.Premise!.Predicate.Predicate.Value);
        Assert.Equal(1, supported.Proof.AdditionalPremises.Length);
        Assert.Equal("dog", supported.Proof.AdditionalPremises[0].Predicate.Predicate.Value);

        var unsupported = engine.Ask(
            knowledgeBase,
            ParseQuestion("Does the blue dog like milk?"));

        Assert.False(unsupported.IsSupported);
        Assert.Null(unsupported.Proof);
    }

    [Fact]
    public void Does_not_infer_a_relational_conclusion_for_an_unmatched_subject_or_object()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog likes milk.");
        Add(knowledgeBase, "Rex is a cat.");

        var unmatchedSubject = engine.Ask(
            knowledgeBase,
            ParseQuestion("Does Rex like milk?"));
        var unmatchedObject = engine.Ask(
            knowledgeBase,
            ParseQuestion("Does Fido like water?"));

        Assert.False(unmatchedSubject.IsSupported);
        Assert.Null(unmatchedSubject.Proof);
        Assert.False(unmatchedObject.IsSupported);
        Assert.Null(unmatchedObject.Proof);
    }

    [Fact]
    public void Processes_a_quantified_relational_rule_fact_and_question_in_one_turn()
    {
        var parsed = new LceParser().ParseText(
            "Every dog likes milk. Fido is a dog. Does Fido like milk?");

        Assert.True(parsed.IsSuccess, parsed.Error);
        Assert.Equal(3, parsed.Utterances.Length);

        var knowledgeBase = new KnowledgeBase();
        ReasoningResult? answer = null;

        foreach (var utterance in parsed.Utterances)
        {
            if (utterance.Kind == UtteranceKind.Assertion)
            {
                var accepted = knowledgeBase.TryAdd(
                    utterance,
                    out _,
                    out var error);
                Assert.True(accepted, error);
                continue;
            }

            var query = Assert.IsType<PredicateApplication>(utterance.Meaning);
            answer = engine.Ask(knowledgeBase, query);
        }

        Assert.NotNull(answer);
        Assert.True(answer.IsSupported);
        Assert.Equal("Every dog likes milk.", answer.Proof!.SourceText);
        Assert.Equal("Fido is a dog.", answer.Proof.Premise!.SourceText);
    }

    [Fact]
    public void Applies_a_relational_rule_after_a_unary_rule_chain()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every animal likes milk.");
        Add(knowledgeBase, "Every dog is an animal.");
        Add(knowledgeBase, "Fido is a dog.");

        var result = engine.Ask(
            knowledgeBase,
            ParseQuestion("Does Fido like milk?"));

        Assert.True(result.IsSupported);
        Assert.Equal("Every animal likes milk.", result.Proof!.SourceText);
        Assert.Equal("Every dog is an animal.", result.Proof.Premise!.SourceText);
        Assert.Equal("Fido is a dog.", result.Proof.Premise.Premise!.SourceText);
    }

    [Fact]
    public void Stores_a_quantified_relational_rule_with_a_concrete_object()
    {
        var knowledgeBase = new KnowledgeBase();

        Assert.Equal(
            KnowledgeEntryKind.Rule,
            Add(knowledgeBase, "Every dog likes milk."));

        var rule = Assert.Single(knowledgeBase.Rules);
        Assert.Equal("dog", Assert.Single(rule.Premises).Predicate.Value);
        Assert.Equal("likes", rule.Conclusion.Predicate.Value);
        Assert.Equal(2, rule.Conclusion.Arguments.Length);
        Assert.Equal(
            "x",
            Assert.IsType<VariableTerm>(rule.Conclusion.Arguments[0]).Identifier.Value);
        Assert.Equal(
            "milk",
            Assert.IsType<IndividualTerm>(rule.Conclusion.Arguments[1]).Identifier.Value);
    }

    [Fact]
    public void Normalizes_a_descriptive_object_in_a_quantified_relational_rule()
    {
        var knowledgeBase = new KnowledgeBase();

        Assert.Equal(
            KnowledgeEntryKind.Rule,
            Add(knowledgeBase, "Every dog likes the red ball."));

        var rule = Assert.Single(knowledgeBase.Rules);
        Assert.Equal(
            "red-ball",
            Assert.IsType<IndividualTerm>(rule.Conclusion.Arguments[1]).Identifier.Value);
        Assert.Contains(knowledgeBase.Facts, fact =>
            fact.Predicate.Predicate.Value == "ball" &&
            Assert.IsType<IndividualTerm>(Assert.Single(fact.Predicate.Arguments)).Identifier.Value == "red-ball");
        Assert.Contains(knowledgeBase.Facts, fact =>
            fact.Predicate.Predicate.Value == "red" &&
            Assert.IsType<IndividualTerm>(Assert.Single(fact.Predicate.Arguments)).Identifier.Value == "red-ball");
    }

    [Fact]
    public void Rejects_a_quantified_relation_with_an_unbound_object_variable()
    {
        var variable = new VariableTerm(new SemanticIdentifier("x"));
        var otherVariable = new VariableTerm(new SemanticIdentifier("y"));
        var meaning = new UniversalQuantification(
            variable,
            new Implication(
                new PredicateApplication(
                    new SemanticIdentifier("dog"),
                    ImmutableArray.Create<SemanticTerm>(variable)),
                new PredicateApplication(
                    new SemanticIdentifier("likes"),
                    ImmutableArray.Create<SemanticTerm>(variable, otherVariable))));
        var utterance = new SemanticUtterance(
            UtteranceKind.Assertion,
            meaning,
            "Every dog likes something.");

        var accepted = new KnowledgeBase().TryAdd(
            utterance,
            out _,
            out var error);

        Assert.False(accepted);
        Assert.NotNull(error);
    }

    [Fact]
    public void Answers_an_exact_relational_query_without_deriving_other_relations()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Fido likes milk.");

        var result = engine.Ask(
            knowledgeBase,
            ParseQuestion("Does Fido like milk?"));

        Assert.True(result.IsSupported);
        Assert.Equal("Fido likes milk.", result.Proof!.SourceText);
        Assert.Equal("likes", result.Proof.Predicate.Predicate.Value);
        Assert.Equal(2, result.Proof.Predicate.Arguments.Length);
        Assert.Null(result.Proof.Premise);

        var differentObject = engine.Ask(
            knowledgeBase,
            ParseQuestion("Does Fido like water?"));

        Assert.False(differentObject.IsSupported);
        Assert.Null(differentObject.Proof);

        var differentSubject = engine.Ask(
            knowledgeBase,
            ParseQuestion("Does Rex like milk?"));

        Assert.False(differentSubject.IsSupported);
        Assert.Null(differentSubject.Proof);
    }

    [Fact]
    public void Stores_and_retrieves_a_relation_using_a_repeated_descriptive_noun_phrase()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Fido likes the red ball.");

        var result = engine.Ask(
            knowledgeBase,
            ParseQuestion("Does Fido like the red ball?"));

        Assert.True(result.IsSupported);
        Assert.Equal("Fido likes the red ball.", result.Proof!.SourceText);
        Assert.Equal("likes(fido, red-ball)", SemanticFormatter.Format(
            new SemanticUtterance(UtteranceKind.Assertion, result.Proof.Predicate, result.Proof.SourceText)));
    }

    [Fact]
    public void Description_head_and_modifiers_become_facts_available_to_rules_and_queries()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every ball is a toy.");
        Add(knowledgeBase, "The red ball is shiny.");

        Assert.Equal(3, knowledgeBase.Facts.Count);
        Assert.Contains(knowledgeBase.Facts, fact =>
            fact.Predicate.Predicate.Value == "shiny" &&
            Assert.IsType<IndividualTerm>(Assert.Single(fact.Predicate.Arguments)).Identifier.Value == "red-ball");
        Assert.Contains(knowledgeBase.Facts, fact =>
            fact.Predicate.Predicate.Value == "ball" &&
            Assert.IsType<IndividualTerm>(Assert.Single(fact.Predicate.Arguments)).Identifier.Value == "red-ball");
        Assert.Contains(knowledgeBase.Facts, fact =>
            fact.Predicate.Predicate.Value == "red" &&
            Assert.IsType<IndividualTerm>(Assert.Single(fact.Predicate.Arguments)).Identifier.Value == "red-ball");

        var headQuestion = engine.Ask(
            knowledgeBase,
            ParseQuestion("Is the red ball a ball?"));
        var modifierQuestion = engine.Ask(
            knowledgeBase,
            ParseQuestion("Is the red ball red?"));
        var inferredQuestion = engine.Ask(
            knowledgeBase,
            ParseQuestion("Is the red ball a toy?"));

        Assert.True(headQuestion.IsSupported);
        Assert.Equal("The red ball is shiny.", headQuestion.Proof!.SourceText);
        Assert.True(modifierQuestion.IsSupported);
        Assert.Equal("The red ball is shiny.", modifierQuestion.Proof!.SourceText);

        Assert.True(inferredQuestion.IsSupported);
        Assert.Equal("Every ball is a toy.", inferredQuestion.Proof!.SourceText);
        Assert.Equal("ball", inferredQuestion.Proof.Premise!.Predicate.Predicate.Value);
        Assert.Equal("The red ball is shiny.", inferredQuestion.Proof.Premise.SourceText);
    }

    [Fact]
    public void Does_not_transfer_a_description_modifier_to_a_different_entity()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "The red ball is shiny.");

        var result = engine.Ask(
            knowledgeBase,
            ParseQuestion("Is the blue ball red?"));

        Assert.False(result.IsSupported);
        Assert.Null(result.Proof);
    }

    [Fact]
    public void Deduplicates_identical_ground_relational_facts()
    {
        var knowledgeBase = new KnowledgeBase();

        Assert.Equal(KnowledgeEntryKind.Fact, Add(knowledgeBase, "Fido likes milk."));
        Assert.Equal(KnowledgeEntryKind.Fact, Add(knowledgeBase, "Fido likes milk."));

        var fact = Assert.Single(knowledgeBase.Facts);
        Assert.Equal("Fido likes milk.", fact.SourceText);
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
}
