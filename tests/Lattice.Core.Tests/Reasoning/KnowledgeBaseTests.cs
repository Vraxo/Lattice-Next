using System.Collections.Generic;
using System.Collections.Immutable;
using AwesomeAssertions;
using Lattice.Core.Language;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Reasoning;

public sealed class KnowledgeBaseTests
{
    [Fact]
    public void Facts_are_exposed_through_a_live_read_only_collection()
    {
        var knowledgeBase = new KnowledgeBase();
        var facts = knowledgeBase.Facts.Should().BeAssignableTo<IList<KnowledgeFact>>().Which;

        facts.IsReadOnly.Should().BeTrue();
        Action clearFacts = () => facts.Clear();
        clearFacts.Should().Throw<NotSupportedException>();

        Add(knowledgeBase, "Fido is a dog.");

        facts.Should().ContainSingle()
            .Which.Predicate.Predicate.Value.Should().Be("dog");
    }

    [Fact]
    public void Rules_are_exposed_through_a_live_read_only_collection()
    {
        var knowledgeBase = new KnowledgeBase();
        var rules = knowledgeBase.Rules.Should().BeAssignableTo<IList<UniversalRule>>().Which;

        rules.IsReadOnly.Should().BeTrue();
        Action clearRules = () => rules.Clear();
        clearRules.Should().Throw<NotSupportedException>();

        Add(knowledgeBase, "Every dog is an animal.");

        rules.Should().ContainSingle()
            .Which.Conclusion.Predicate.Value.Should().Be("animal");
    }

    [Fact]
    public void Accepts_one_fixed_object_relational_premise()
    {
        var knowledgeBase = new KnowledgeBase();
        var utterance = CreateRule(
            CreateRelation("likes", "milk"));

        var accepted = knowledgeBase.TryAdd(utterance, out var kind, out var error);

        accepted.Should().BeTrue(error);
        kind.Should().Be(KnowledgeEntryKind.Rule);

        knowledgeBase.Rules.Should().ContainSingle()
            .Which.RelationalPremises.Should().ContainSingle()
            .Which.Predicate.Value.Should().Be("likes");
    }

    [Fact]
    public void Rejects_multiple_fixed_object_relational_premises()
    {
        var knowledgeBase = new KnowledgeBase();
        var utterance = CreateRule(
            CreateRelation("likes", "milk"),
            CreateRelation("chases", "cats"));

        var accepted = knowledgeBase.TryAdd(utterance, out _, out var error);

        accepted.Should().BeFalse();
        error.Should().Contain("supported");
        knowledgeBase.Rules.Should().BeEmpty();
    }

    [Fact]
    public void Does_not_accept_questions_or_relations_with_unresolved_arguments_as_facts()
    {
        var knowledgeBase = new KnowledgeBase();

        var questionResult = new LceParser().Parse("Is Fido a dog?");
        questionResult.IsSuccess.Should().BeTrue(questionResult.Error);
        var questionAccepted = knowledgeBase.TryAdd(
            questionResult.Utterance.Should().BeOfType<SemanticUtterance>().Which,
            out _,
            out var questionError);

        questionAccepted.Should().BeFalse();
        questionError.Should().NotBeNull();

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

        relationAccepted.Should().BeFalse();
        relationError.Should().NotBeNull();
    }

    private static SemanticUtterance CreateRule(params PredicateApplication[] relationalPremises)
    {
        var variable = new VariableTerm(new SemanticIdentifier("x"));
        var premises = ImmutableArray.CreateBuilder<SemanticFormula>();
        premises.Add(CreateUnary("dog", variable));
        premises.AddRange(relationalPremises);

        var meaning = new UniversalQuantification(
            variable,
            new Implication(
                new Conjunction(premises.ToImmutable()),
                CreateUnary("friendly", variable)));

        return new SemanticUtterance(
            UtteranceKind.Assertion,
            meaning,
            "Every dog with the specified conditions is friendly.");
    }

    private static PredicateApplication CreateRelation(string predicate, string objectIdentifier)
    {
        var variable = new VariableTerm(new SemanticIdentifier("x"));
        return new PredicateApplication(
            new SemanticIdentifier(predicate),
            ImmutableArray.Create<SemanticTerm>(
                variable,
                new IndividualTerm(new SemanticIdentifier(objectIdentifier))));
    }

    private static PredicateApplication CreateUnary(string predicate, SemanticTerm individual) =>
        new(
            new SemanticIdentifier(predicate),
            ImmutableArray.Create(individual));

    private static void Add(KnowledgeBase knowledgeBase, string sentence)
    {
        var parsed = new LceParser().Parse(sentence);
        parsed.IsSuccess.Should().BeTrue(parsed.Error);
        parsed.Utterance.Should().NotBeNull();

        var accepted = knowledgeBase.TryAdd(
            parsed.Utterance,
            out _,
            out var error);

        accepted.Should().BeTrue(error);
    }
}
