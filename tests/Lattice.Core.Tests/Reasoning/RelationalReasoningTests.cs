using System.Collections.Immutable;
using Lattice.Core.Language;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using AwesomeAssertions;
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
        result.TruthValue.Should().Be(TruthValue.True);
        result.Proof!.SourceText.Should().Be("Every dog that likes milk is friendly.");
        result.Proof.Premise!.SourceText.Should().Be("Fido is a dog.");
        var relation = result.Proof.AdditionalPremises.Should().ContainSingle().Which;
        relation.Predicate.Predicate.Value.Should().Be("likes");
        relation.SourceText.Should().Be("Fido likes milk.");
    }

    [Fact]
    public void Missing_or_negative_relations_do_not_satisfy_positive_rule_premises()
    {
        var kb = new KnowledgeBase();
        Add(kb, "Every dog that likes milk is friendly.");
        Add(kb, "Fido is a dog.");
        engine.Ask(kb, ParseQuestion("Is Fido friendly?")).TruthValue.Should().Be(TruthValue.Unknown);
        Add(kb, "Fido does not like milk.");
        engine.Ask(kb, ParseQuestion("Is Fido friendly?")).TruthValue.Should().Be(TruthValue.Unknown);
    }

    [Fact]
    public void Relational_premises_must_match_the_same_individual()
    {
        var kb = new KnowledgeBase();
        Add(kb, "Every dog that likes milk is friendly.");
        Add(kb, "Fido is a dog.");
        Add(kb, "Rex likes milk.");
        engine.Ask(kb, ParseQuestion("Is Fido friendly?")).TruthValue.Should().Be(TruthValue.Unknown);
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

        result.IsSupported.Should().BeTrue();
        var proof = result.Proof.Should().BeOfType<ProofNode>().Which;
        proof.Predicate.Predicate.Value.Should().Be("likes");
        proof.SourceText.Should().Be("Every dog likes milk.");
        proof.Kind.Should().Be(ProofNodeKind.Derived);
        proof.Predicate.Arguments[0].Should().BeOfType<IndividualTerm>().Which.Identifier.Value.Should().Be("fido");
        proof.Predicate.Arguments[1].Should().BeOfType<IndividualTerm>().Which.Identifier.Value.Should().Be("milk");

        var premise = proof.Premise.Should().BeOfType<ProofNode>().Which;
        premise.SourceText.Should().Be("Fido is a dog.");
        premise.Predicate.Predicate.Value.Should().Be("dog");
        premise.Kind.Should().Be(ProofNodeKind.Asserted);
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

        supported.IsSupported.Should().BeTrue();
        supported.Proof!.SourceText.Should().Be("Every red dog likes milk.");
        supported.Proof.Premise!.Predicate.Predicate.Value.Should().Be("red");
        supported.Proof.AdditionalPremises.Length.Should().Be(1);
        supported.Proof.AdditionalPremises[0].Predicate.Predicate.Value.Should().Be("dog");

        var unsupported = engine.Ask(
            knowledgeBase,
            ParseQuestion("Does the blue dog like milk?"));

        unsupported.IsSupported.Should().BeFalse();
        unsupported.Proof.Should().BeNull();
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

        unmatchedSubject.IsSupported.Should().BeFalse();
        unmatchedSubject.Proof.Should().BeNull();
        unmatchedObject.IsSupported.Should().BeFalse();
        unmatchedObject.Proof.Should().BeNull();
    }

    [Fact]
    public void Processes_a_quantified_relational_rule_fact_and_question_in_one_turn()
    {
        var parsed = new LceParser().ParseText(
            "Every dog likes milk. Fido is a dog. Does Fido like milk?");

        parsed.IsSuccess.Should().BeTrue(parsed.Error);
        parsed.Utterances.Length.Should().Be(3);

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
                accepted.Should().BeTrue(error);
                continue;
            }

            var query = utterance.Meaning.Should().BeOfType<PredicateApplication>().Which;
            answer = engine.Ask(knowledgeBase, query);
        }

        answer.Should().NotBeNull();
        answer.IsSupported.Should().BeTrue();
        answer.Proof!.SourceText.Should().Be("Every dog likes milk.");
        answer.Proof.Premise!.SourceText.Should().Be("Fido is a dog.");
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

        result.IsSupported.Should().BeTrue();
        result.Proof!.SourceText.Should().Be("Every animal likes milk.");
        result.Proof.Premise!.SourceText.Should().Be("Every dog is an animal.");
        result.Proof.Premise.Premise!.SourceText.Should().Be("Fido is a dog.");
    }

    [Fact]
    public void Stores_a_quantified_relational_rule_with_a_concrete_object()
    {
        var knowledgeBase = new KnowledgeBase();

        Add(knowledgeBase, "Every dog likes milk.").Should().Be(KnowledgeEntryKind.Rule);

        var rule = knowledgeBase.Rules.Should().ContainSingle().Which;
        rule.Premises.Should().ContainSingle().Which.Predicate.Value.Should().Be("dog");
        rule.Conclusion.Predicate.Value.Should().Be("likes");
        rule.Conclusion.Arguments.Length.Should().Be(2);
        rule.Conclusion.Arguments[0].Should().BeOfType<VariableTerm>().Which.Identifier.Value.Should().Be("x");
        rule.Conclusion.Arguments[1].Should().BeOfType<IndividualTerm>().Which.Identifier.Value.Should().Be("milk");
    }

    [Fact]
    public void Normalizes_a_descriptive_object_in_a_quantified_relational_rule()
    {
        var knowledgeBase = new KnowledgeBase();

        Add(knowledgeBase, "Every dog likes the red ball.").Should().Be(KnowledgeEntryKind.Rule);

        var rule = knowledgeBase.Rules.Should().ContainSingle().Which;
        rule.Conclusion.Arguments[1].Should().BeOfType<IndividualTerm>().Which.Identifier.Value.Should().Be("red-ball");
        knowledgeBase.Facts.Should().Contain(fact =>
            fact.Predicate.Predicate.Value == "ball" &&
            fact.Predicate.Arguments.Should().ContainSingle().Which.Should().BeOfType<IndividualTerm>().Which.Identifier.Value == "red-ball");
        knowledgeBase.Facts.Should().Contain(fact =>
            fact.Predicate.Predicate.Value == "red" &&
            fact.Predicate.Arguments.Should().ContainSingle().Which.Should().BeOfType<IndividualTerm>().Which.Identifier.Value == "red-ball");
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

        accepted.Should().BeFalse();
        error.Should().NotBeNull();
    }

    [Fact]
    public void Answers_an_exact_relational_query_without_deriving_other_relations()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Fido likes milk.");

        var result = engine.Ask(
            knowledgeBase,
            ParseQuestion("Does Fido like milk?"));

        result.IsSupported.Should().BeTrue();
        result.Proof!.SourceText.Should().Be("Fido likes milk.");
        result.Proof.Predicate.Predicate.Value.Should().Be("likes");
        result.Proof.Predicate.Arguments.Length.Should().Be(2);
        result.Proof.Premise.Should().BeNull();

        var differentObject = engine.Ask(
            knowledgeBase,
            ParseQuestion("Does Fido like water?"));

        differentObject.IsSupported.Should().BeFalse();
        differentObject.Proof.Should().BeNull();

        var differentSubject = engine.Ask(
            knowledgeBase,
            ParseQuestion("Does Rex like milk?"));

        differentSubject.IsSupported.Should().BeFalse();
        differentSubject.Proof.Should().BeNull();
    }

    [Fact]
    public void Stores_and_retrieves_a_relation_using_a_repeated_descriptive_noun_phrase()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Fido likes the red ball.");

        var result = engine.Ask(
            knowledgeBase,
            ParseQuestion("Does Fido like the red ball?"));

        result.IsSupported.Should().BeTrue();
        result.Proof!.SourceText.Should().Be("Fido likes the red ball.");
        SemanticFormatter.Format(
            new SemanticUtterance(UtteranceKind.Assertion, result.Proof.Predicate, result.Proof.SourceText)).Should().Be("likes(fido, red-ball)");
    }

    [Fact]
    public void Description_head_and_modifiers_become_facts_available_to_rules_and_queries()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every ball is a toy.");
        Add(knowledgeBase, "The red ball is shiny.");

        knowledgeBase.Facts.Count.Should().Be(3);
        knowledgeBase.Facts.Should().Contain(fact =>
            fact.Predicate.Predicate.Value == "shiny" &&
            fact.Predicate.Arguments.Should().ContainSingle().Which.Should().BeOfType<IndividualTerm>().Which.Identifier.Value == "red-ball");
        knowledgeBase.Facts.Should().Contain(fact =>
            fact.Predicate.Predicate.Value == "ball" &&
            fact.Predicate.Arguments.Should().ContainSingle().Which.Should().BeOfType<IndividualTerm>().Which.Identifier.Value == "red-ball");
        knowledgeBase.Facts.Should().Contain(fact =>
            fact.Predicate.Predicate.Value == "red" &&
            fact.Predicate.Arguments.Should().ContainSingle().Which.Should().BeOfType<IndividualTerm>().Which.Identifier.Value == "red-ball");

        var headQuestion = engine.Ask(
            knowledgeBase,
            ParseQuestion("Is the red ball a ball?"));
        var modifierQuestion = engine.Ask(
            knowledgeBase,
            ParseQuestion("Is the red ball red?"));
        var inferredQuestion = engine.Ask(
            knowledgeBase,
            ParseQuestion("Is the red ball a toy?"));

        headQuestion.IsSupported.Should().BeTrue();
        headQuestion.Proof!.SourceText.Should().Be("The red ball is shiny.");
        modifierQuestion.IsSupported.Should().BeTrue();
        modifierQuestion.Proof!.SourceText.Should().Be("The red ball is shiny.");

        inferredQuestion.IsSupported.Should().BeTrue();
        inferredQuestion.Proof!.SourceText.Should().Be("Every ball is a toy.");
        inferredQuestion.Proof.Premise!.Predicate.Predicate.Value.Should().Be("ball");
        inferredQuestion.Proof.Premise.SourceText.Should().Be("The red ball is shiny.");
    }

    [Fact]
    public void Does_not_transfer_a_description_modifier_to_a_different_entity()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "The red ball is shiny.");

        var result = engine.Ask(
            knowledgeBase,
            ParseQuestion("Is the blue ball red?"));

        result.IsSupported.Should().BeFalse();
        result.Proof.Should().BeNull();
    }

    [Fact]
    public void Deduplicates_identical_ground_relational_facts()
    {
        var knowledgeBase = new KnowledgeBase();

        Add(knowledgeBase, "Fido likes milk.").Should().Be(KnowledgeEntryKind.Fact);
        Add(knowledgeBase, "Fido likes milk.").Should().Be(KnowledgeEntryKind.Fact);

        var fact = knowledgeBase.Facts.Should().ContainSingle().Which;
        fact.SourceText.Should().Be("Fido likes milk.");
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

        positiveQuery.TruthValue.Should().Be(TruthValue.False);
        positiveQuery.Proof.Should().BeNull();
        positiveQuery.ContradictingProofs.Should().ContainSingle().Which.SourceText.Should().Be("Fido does not like milk.");
        negativeQuery.TruthValue.Should().Be(TruthValue.True);
        negativeQuery.Proof!.Polarity.Should().Be(EvidencePolarity.Negative);
        differentObject.TruthValue.Should().Be(TruthValue.Unknown);
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

        result.TruthValue.Should().Be(TruthValue.Both);
        result.Proof!.SourceText.Should().Be("Fido likes milk.");
        result.ContradictingProofs.Should().ContainSingle().Which.SourceText.Should().Be("Fido does not like milk.");
        knowledgeBase.Facts.Count.Should().Be(2);
    }

    [Fact]
    public void Uses_a_derived_negative_premise_for_a_fixed_object_relational_conclusion()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog is not a cat.");
        Add(knowledgeBase, "Every dog that is not a cat does not like milk.");
        Add(knowledgeBase, "Fido is a dog.");

        var result = engine.Ask(knowledgeBase, ParseQuestion("Does Fido like milk?"));

        result.TruthValue.Should().Be(TruthValue.False);
        var negativeRelation = result.ContradictingProofs.Should().ContainSingle().Which;
        negativeRelation.SourceText.Should().Be("Every dog that is not a cat does not like milk.");

        var negativePremise = negativeRelation.AdditionalPremises.Should().ContainSingle().Which;
        negativePremise.SourceText.Should().Be("Every dog is not a cat.");
        negativePremise.Polarity.Should().Be(EvidencePolarity.Negative);
        negativePremise.Premise!.SourceText.Should().Be("Fido is a dog.");
    }

    [Fact]
    public void Derives_negative_fixed_object_relations_from_positive_unary_premises()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog does not like milk.");
        Add(knowledgeBase, "Fido is a dog.");

        var result = engine.Ask(knowledgeBase, ParseQuestion("Does Fido like milk?"));

        result.TruthValue.Should().Be(TruthValue.False);
        result.ContradictingProofs.Should().ContainSingle().Which.SourceText.Should().Be("Every dog does not like milk.");
    }

    [Fact]
    public void Applies_negated_premises_to_fixed_object_relational_conclusions()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog that is not a cat likes milk.");
        Add(knowledgeBase, "Fido is a dog.");
        Add(knowledgeBase, "Fido is not a cat.");

        var result = engine.Ask(knowledgeBase, ParseQuestion("Does Fido like milk?"));

        result.TruthValue.Should().Be(TruthValue.True);
        result.Proof!.SourceText.Should().Be("Every dog that is not a cat likes milk.");
        result.Proof.AdditionalPremises.Single().SourceText.Should().Be("Fido is not a cat.");
    }
}
