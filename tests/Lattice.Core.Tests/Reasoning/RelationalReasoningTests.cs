using System.Collections.Immutable;
using Lattice.Core.Language;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using Xunit;

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
    public void Deduplicates_identical_ground_relational_facts()
    {
        var knowledgeBase = new KnowledgeBase();

        Assert.Equal(KnowledgeEntryKind.Fact, Add(knowledgeBase, "Fido likes milk."));
        Assert.Equal(KnowledgeEntryKind.Fact, Add(knowledgeBase, "Fido likes milk."));

        var fact = Assert.Single(knowledgeBase.Facts);
        Assert.Equal("Fido likes milk.", fact.SourceText);
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
