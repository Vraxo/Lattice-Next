using Lattice.Core.Language;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using AwesomeAssertions;
using Xunit;
using static Lattice.Core.Tests.Reasoning.ReasoningTestSupport;

namespace Lattice.Core.Tests.Reasoning;

public sealed class ExistentialReasoningTests
{
    private readonly ReasoningEngine engine = new();

    [Fact]
    public void Each_subject_gets_its_own_existential_relation_witness()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every polygon has a boundary.");
        Add(knowledgeBase, "PolygonOne is a polygon.");
        Add(knowledgeBase, "PolygonTwo is a polygon.");

        var firstSubject = new IndividualTerm(new SemanticIdentifier("polygonone"));
        var secondSubject = new IndividualTerm(new SemanticIdentifier("polygontwo"));

        var firstBoundaries = engine.AskRelated(
            knowledgeBase,
            new SemanticIdentifier("has"),
            firstSubject);
        var secondBoundaries = engine.AskRelated(
            knowledgeBase,
            new SemanticIdentifier("has"),
            secondSubject);

        firstBoundaries.Proofs.Should().ContainSingle();
        secondBoundaries.Proofs.Should().ContainSingle();

        var firstWitness = firstBoundaries.Proof!.Predicate.Arguments[1]
            .Should().BeOfType<AnonymousIndividualTerm>().Which;
        var secondWitness = secondBoundaries.Proof!.Predicate.Arguments[1]
            .Should().BeOfType<AnonymousIndividualTerm>().Which;

        firstWitness.Should().NotBe(secondWitness);

        var firstIsBoundary = engine.Ask(
            knowledgeBase,
            new PredicateApplication(
                new SemanticIdentifier("boundary"),
                System.Collections.Immutable.ImmutableArray.Create<SemanticTerm>(firstWitness)));
        var secondIsBoundary = engine.Ask(
            knowledgeBase,
            new PredicateApplication(
                new SemanticIdentifier("boundary"),
                System.Collections.Immutable.ImmutableArray.Create<SemanticTerm>(secondWitness)));

        firstIsBoundary.IsSupported.Should().BeTrue();
        secondIsBoundary.IsSupported.Should().BeTrue();
        firstIsBoundary.Proof!.Kind.Should().Be(ProofNodeKind.Derived);
        firstIsBoundary.Proof.Sources.Should().Contain(source =>
            source.SourceText == "Every polygon has a boundary.");
    }

    [Fact]
    public void Answers_existence_from_one_anonymous_witness_without_naming_it()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "A red ball is shiny.");

        knowledgeBase.Facts.Count.Should().Be(3);
        var witnesses = knowledgeBase.Facts
            .Select(fact => fact.Predicate.Arguments.Should().ContainSingle().Which.Should().BeOfType<AnonymousIndividualTerm>().Which.Identity)
            .Distinct()
            .ToArray();
        witnesses.Should().ContainSingle();

        var existence = engine.Ask(
            knowledgeBase,
            ParseExistentialQuestion("Does a red ball exist?"));

        existence.IsSupported.Should().BeTrue();
        var existenceProof = existence.Proof.Should().BeOfType<ProofNode>().Which;
        existenceProof.SourceText.Should().Be("A red ball is shiny.");
        existenceProof.Predicate.Predicate.Value.Should().Be("red");
        existenceProof.AdditionalPremises.Should().ContainSingle().Which
            .Predicate.Predicate.Value.Should().Be("ball");

        var differentDescription = engine.Ask(
            knowledgeBase,
            ParseExistentialQuestion("Does a blue ball exist?"));

        differentDescription.IsSupported.Should().BeFalse();
        differentDescription.Proof.Should().BeNull();
    }

    [Fact]
    public void A_named_descriptive_individual_can_support_an_existence_query()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "The red ball is shiny.");

        var result = engine.Ask(
            knowledgeBase,
            ParseExistentialQuestion("Does a red ball exist?"));

        result.IsSupported.Should().BeTrue();
        result.Proof!.SourceText.Should().Be("The red ball is shiny.");
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

        result.IsSupported.Should().BeTrue();
        var proof = result.Proof.Should().BeOfType<ProofNode>().Which;
        proof.SourceText.Should().Be("Every red ball is a toy.");
        proof.Premise!.SourceText.Should().Be("A red ball is shiny.");
    }

    [Fact]
    public void Does_not_combine_properties_from_separate_existential_witnesses()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "A red ball is shiny.");
        Add(knowledgeBase, "A blue ball is round.");

        var witnesses = knowledgeBase.Facts
            .Select(fact => fact.Predicate.Arguments.Should().ContainSingle().Which.Should().BeOfType<AnonymousIndividualTerm>().Which.Identity)
            .Distinct()
            .ToArray();
        witnesses.Length.Should().Be(2);

        var result = engine.Ask(
            knowledgeBase,
            ParseExistentialQuestion("Does a red round ball exist?"));

        result.IsSupported.Should().BeFalse();
        result.Proof.Should().BeNull();
    }

    [Fact]
    public void Does_not_treat_anonymous_existential_witness_as_a_named_definite_individual()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "A red ball is shiny.");

        var result = engine.Ask(
            knowledgeBase,
            ParseQuestion("Is the red ball shiny?"));

        result.IsSupported.Should().BeFalse();
        result.Proof.Should().BeNull();
    }

    [Fact]
    public void Processes_an_existential_assertion_and_question_in_one_turn()
    {
        var parsed = new LceParser().ParseText(
            "A red ball is shiny. Does a red ball exist?");

        parsed.IsSuccess.Should().BeTrue(parsed.Error);
        parsed.Utterances.Length.Should().Be(2);

        var knowledgeBase = new KnowledgeBase();
        ReasoningResult? answer = null;

        foreach (var utterance in parsed.Utterances)
        {
            if (utterance.Kind == UtteranceKind.Assertion)
            {
                var accepted = knowledgeBase.TryAdd(utterance, out _, out var error);
                accepted.Should().BeTrue(error);
                continue;
            }

            var query = utterance.Meaning.Should().BeOfType<ExistentialQuantification>().Which;
            answer = engine.Ask(knowledgeBase, query);
        }

        answer.Should().NotBeNull();
        answer.IsSupported.Should().BeTrue();
        answer.Proof!.SourceText.Should().Be("A red ball is shiny.");
    }
}
