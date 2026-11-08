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
