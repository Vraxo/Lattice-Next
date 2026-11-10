using System.Collections.Generic;
using System.Collections.Immutable;
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
        var facts = Assert.IsAssignableFrom<IList<KnowledgeFact>>(knowledgeBase.Facts);

        Assert.True(facts.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => facts.Clear());

        Add(knowledgeBase, "Fido is a dog.");

        Assert.Single(facts);
        Assert.Equal("dog", facts[0].Predicate.Predicate.Value);
    }

    [Fact]
    public void Rules_are_exposed_through_a_live_read_only_collection()
    {
        var knowledgeBase = new KnowledgeBase();
        var rules = Assert.IsAssignableFrom<IList<UniversalRule>>(knowledgeBase.Rules);

        Assert.True(rules.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => rules.Clear());

        Add(knowledgeBase, "Every dog is an animal.");

        Assert.Single(rules);
        Assert.Equal("animal", rules[0].Conclusion.Predicate.Value);
    }

    [Fact]
    public void Accepts_one_fixed_object_relational_premise()
    {
        var knowledgeBase = new KnowledgeBase();
        var utterance = CreateRule(
            CreateRelation("likes", "milk"));

        var accepted = knowledgeBase.TryAdd(utterance, out var kind, out var error);

        Assert.True(accepted, error);
        Assert.Equal(KnowledgeEntryKind.Rule, kind);
        var rule = Assert.Single(knowledgeBase.Rules);
        Assert.Equal("likes", Assert.Single(rule.RelationalPremises).Predicate.Value);
    }

    [Fact]
    public void Rejects_multiple_fixed_object_relational_premises()
    {
        var knowledgeBase = new KnowledgeBase();
        var utterance = CreateRule(
            CreateRelation("likes", "milk"),
            CreateRelation("chases", "cats"));

        var accepted = knowledgeBase.TryAdd(utterance, out _, out var error);

        Assert.False(accepted);
        Assert.Contains("supported", error);
        Assert.Empty(knowledgeBase.Rules);
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
        Assert.True(parsed.IsSuccess, parsed.Error);
        Assert.NotNull(parsed.Utterance);

        var accepted = knowledgeBase.TryAdd(
            parsed.Utterance,
            out _,
            out var error);

        Assert.True(accepted, error);
    }
}
