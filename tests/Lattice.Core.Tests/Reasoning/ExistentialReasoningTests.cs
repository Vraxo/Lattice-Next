using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using Xunit;
using static Lattice.Core.Tests.Reasoning.ReasoningTestSupport;

namespace Lattice.Core.Tests.Reasoning;

public sealed class ExistentialReasoningTests
{
    private readonly ReasoningEngine engine = new();

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
}
