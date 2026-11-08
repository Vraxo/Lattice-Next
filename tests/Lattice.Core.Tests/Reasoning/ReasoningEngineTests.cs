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
