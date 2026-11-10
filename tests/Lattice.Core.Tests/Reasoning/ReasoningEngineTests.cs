using System.Collections.Immutable;
using Lattice.Core.Language;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Reasoning;

public sealed class ReasoningEngineTests
{
    private readonly ReasoningEngine engine = new();

    [Fact]
    public void Infers_a_supported_fact_and_preserves_its_explanation()
    {
        var knowledgeBase = new KnowledgeBase();

        Assert.Equal(
            KnowledgeEntryKind.Rule,
            Add(knowledgeBase, "Every dog is an animal."));
        Assert.Equal(
            KnowledgeEntryKind.Fact,
            Add(knowledgeBase, "Fido is a dog."));

        var result = engine.Ask(knowledgeBase, ParseQuestion("Is Fido an animal?"));

        Assert.True(result.IsSupported);

        var proof = Assert.IsType<ProofNode>(result.Proof);
        Assert.Equal("animal", proof.Predicate.Predicate.Value);
        Assert.Equal("Every dog is an animal.", proof.SourceText);

        var premise = Assert.IsType<ProofNode>(proof.Premise);
        Assert.Equal("dog", premise.Predicate.Predicate.Value);
        Assert.Equal("Fido is a dog.", premise.SourceText);
        Assert.Null(premise.Premise);
    }

    [Fact]
    public void Retains_multiple_source_kinds_for_one_deduplicated_fact()
    {
        var knowledgeBase = new KnowledgeBase();
        var assertion = Parse("Fido is a dog.");

        Assert.True(knowledgeBase.TryAdd(assertion, out _, out var userError), userError);
        Assert.True(knowledgeBase.TryAdd(
            assertion,
            out _,
            out var observationError,
            KnowledgeSourceKind.ExternalObservation), observationError);

        var fact = Assert.Single(knowledgeBase.Facts);
        Assert.Equal(2, fact.Sources.Length);
        Assert.Contains(fact.Sources, source =>
            source.Kind == KnowledgeSourceKind.UserAssertion);
        Assert.Contains(fact.Sources, source =>
            source.Kind == KnowledgeSourceKind.ExternalObservation);

        var result = engine.Ask(knowledgeBase, ParseQuestion("Is Fido a dog?"));
        Assert.True(result.IsSupported);
        Assert.Equal(2, result.Proof!.Sources.Length);
        Assert.All(result.Proof.Sources, source =>
            Assert.Equal("Fido is a dog.", source.SourceText));
        Assert.Equal(ProofNodeKind.Asserted, result.Proof.Kind);
    }

    [Fact]
    public void Marks_rule_conclusions_as_derived_and_retains_rule_sources()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog is an animal.");
        Add(knowledgeBase, "Fido is a dog.");

        var result = engine.Ask(knowledgeBase, ParseQuestion("Is Fido an animal?"));

        Assert.True(result.IsSupported);
        Assert.Equal(ProofNodeKind.Derived, result.Proof!.Kind);
        Assert.Equal(KnowledgeSourceKind.UserAssertion, result.Proof.Sources[0].Kind);
        Assert.Equal(ProofNodeKind.Asserted, result.Proof.Premise!.Kind);
    }

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
    public void Returns_unknown_for_an_empty_knowledge_base()
    {
        var result = engine.Ask(
            new KnowledgeBase(),
            ParseQuestion("Is Fido a reptile?"));

        Assert.False(result.IsSupported);
    }

    [Fact]
    public void Does_not_accept_questions_or_relations_with_unresolved_arguments_as_facts()
    {
        var knowledgeBase = new KnowledgeBase();

        var questionAccepted = knowledgeBase.TryAdd(
            Parse("Is Fido a dog?"),
            out _,
            out var questionError);

        Assert.False(questionAccepted);
        Assert.NotNull(questionError);

        var relation = new PredicateApplication(
            new SemanticIdentifier("likes"),
            ImmutableArray.Create<SemanticTerm>(
                new IndividualTerm(new SemanticIdentifier("fido")),
                new UnresolvedReferenceTerm(new SemanticIdentifier("it"))));
        var relationUtterance = new SemanticUtterance(
            UtteranceKind.Assertion,
            relation,
            "Fido likes it.");

        var relationAccepted = knowledgeBase.TryAdd(
            relationUtterance,
            out _,
            out var relationError);

        Assert.False(relationAccepted);
        Assert.NotNull(relationError);
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

    [Fact]
    public void Answers_existence_from_one_anonymous_witness_without_naming_it()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "A red ball is shiny.");

        Assert.Equal(3, knowledgeBase.Facts.Count);
        var witnesses = knowledgeBase.Facts
            .Select(fact => Assert.IsType<AnonymousIndividualTerm>(
                Assert.Single(fact.Predicate.Arguments)).Identity)
            .Distinct()
            .ToArray();
        Assert.Single(witnesses);

        var existence = engine.Ask(
            knowledgeBase,
            ParseExistentialQuestion("Does a red ball exist?"));

        Assert.True(existence.IsSupported);
        var existenceProof = Assert.IsType<ProofNode>(existence.Proof);
        Assert.Equal("A red ball is shiny.", existenceProof.SourceText);
        Assert.Equal("red", existenceProof.Predicate.Predicate.Value);
        Assert.Equal("ball", Assert.Single(existenceProof.AdditionalPremises)
            .Predicate.Predicate.Value);

        var differentDescription = engine.Ask(
            knowledgeBase,
            ParseExistentialQuestion("Does a blue ball exist?"));

        Assert.False(differentDescription.IsSupported);
        Assert.Null(differentDescription.Proof);
    }

    [Fact]
    public void A_named_descriptive_individual_can_support_an_existence_query()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "The red ball is shiny.");

        var result = engine.Ask(
            knowledgeBase,
            ParseExistentialQuestion("Does a red ball exist?"));

        Assert.True(result.IsSupported);
        Assert.Equal("The red ball is shiny.", result.Proof!.SourceText);
    }

    [Fact]
    public void Existential_witnesses_can_satisfy_universal_rules()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every red ball is a toy.");
        Add(knowledgeBase, "A red ball is shiny.");

        var result = engine.Ask(
            knowledgeBase,
            ParseExistentialQuestion("Does a toy exist?"));

        Assert.True(result.IsSupported);
        var proof = Assert.IsType<ProofNode>(result.Proof);
        Assert.Equal("Every red ball is a toy.", proof.SourceText);
        Assert.Equal("A red ball is shiny.", proof.Premise!.SourceText);
    }

    [Fact]
    public void Does_not_combine_properties_from_separate_existential_witnesses()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "A red ball is shiny.");
        Add(knowledgeBase, "A blue ball is round.");

        var witnesses = knowledgeBase.Facts
            .Select(fact => Assert.IsType<AnonymousIndividualTerm>(
                Assert.Single(fact.Predicate.Arguments)).Identity)
            .Distinct()
            .ToArray();
        Assert.Equal(2, witnesses.Length);

        var result = engine.Ask(
            knowledgeBase,
            ParseExistentialQuestion("Does a red round ball exist?"));

        Assert.False(result.IsSupported);
        Assert.Null(result.Proof);
    }

    [Fact]
    public void Does_not_treat_anonymous_existential_witness_as_a_named_definite_individual()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "A red ball is shiny.");

        var result = engine.Ask(
            knowledgeBase,
            ParseQuestion("Is the red ball shiny?"));

        Assert.False(result.IsSupported);
        Assert.Null(result.Proof);
    }

    [Fact]
    public void Processes_an_existential_assertion_and_question_in_one_turn()
    {
        var parsed = new LceParser().ParseText(
            "A red ball is shiny. Does a red ball exist?");

        Assert.True(parsed.IsSuccess, parsed.Error);
        Assert.Equal(2, parsed.Utterances.Length);

        var knowledgeBase = new KnowledgeBase();
        ReasoningResult? answer = null;

        foreach (var utterance in parsed.Utterances)
        {
            if (utterance.Kind == UtteranceKind.Assertion)
            {
                var accepted = knowledgeBase.TryAdd(utterance, out _, out var error);
                Assert.True(accepted, error);
                continue;
            }

            var query = Assert.IsType<ExistentialQuantification>(utterance.Meaning);
            answer = engine.Ask(knowledgeBase, query);
        }

        Assert.NotNull(answer);
        Assert.True(answer.IsSupported);
        Assert.Equal("A red ball is shiny.", answer.Proof!.SourceText);
    }

    [Fact]
    public void Retains_independent_rule_paths_for_the_same_conclusion()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog is a pet.");
        Add(knowledgeBase, "Every animal is a pet.");
        Add(knowledgeBase, "Every dog is an animal.");
        Add(knowledgeBase, "Fido is a dog.");

        var result = engine.Ask(
            knowledgeBase,
            ParseQuestion("Is Fido a pet?"));

        Assert.Equal(2, result.Proofs.Length);
        Assert.Contains(result.Proofs, proof =>
            proof.SourceText == "Every dog is a pet." &&
            proof.Premise?.SourceText == "Fido is a dog.");
        Assert.Contains(result.Proofs, proof =>
            proof.SourceText == "Every animal is a pet." &&
            proof.Premise?.SourceText == "Every dog is an animal.");
        Assert.All(result.Proofs, proof => Assert.Equal(ProofNodeKind.Derived, proof.Kind));
    }

    [Fact]
    public void Retains_both_direct_and_derived_support_for_one_fact()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog is an animal.");
        Add(knowledgeBase, "Fido is a dog.");
        Add(knowledgeBase, "Fido is an animal.");

        var result = engine.Ask(
            knowledgeBase,
            ParseQuestion("Is Fido an animal?"));

        Assert.Equal(2, result.Proofs.Length);
        Assert.Contains(result.Proofs, proof => proof.Kind == ProofNodeKind.Asserted);
        Assert.Contains(result.Proofs, proof =>
            proof.Kind == ProofNodeKind.Derived &&
            proof.SourceText == "Every dog is an animal.");
    }

    [Fact]
    public void Does_not_create_cyclic_proofs_when_rules_refer_to_each_other()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog is an animal.");
        Add(knowledgeBase, "Every animal is a dog.");
        Add(knowledgeBase, "Fido is a dog.");

        var animal = engine.Ask(
            knowledgeBase,
            ParseQuestion("Is Fido an animal?"));
        var dog = engine.Ask(
            knowledgeBase,
            ParseQuestion("Is Fido a dog?"));

        var animalProof = Assert.Single(animal.Proofs);
        var dogProof = Assert.Single(dog.Proofs);
        Assert.Equal("Every dog is an animal.", animalProof.SourceText);
        Assert.Equal(ProofNodeKind.Asserted, dogProof.Kind);
    }

    [Fact]
    public void Retains_independent_relational_rule_paths()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog likes milk.");
        Add(knowledgeBase, "Every animal likes milk.");
        Add(knowledgeBase, "Every dog is an animal.");
        Add(knowledgeBase, "Fido is a dog.");

        var result = engine.Ask(
            knowledgeBase,
            ParseQuestion("Does Fido like milk?"));

        Assert.Equal(2, result.Proofs.Length);
        Assert.Contains(result.Proofs, proof =>
            proof.SourceText == "Every dog likes milk.");
        Assert.Contains(result.Proofs, proof =>
            proof.SourceText == "Every animal likes milk." &&
            proof.Premise?.SourceText == "Every dog is an animal.");
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
