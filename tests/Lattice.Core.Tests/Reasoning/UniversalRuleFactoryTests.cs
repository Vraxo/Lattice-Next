using System.Collections.Immutable;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Reasoning;

public sealed class UniversalRuleFactoryTests
{
    [Fact]
    public void Creates_rules_with_separate_positive_negative_and_relational_premises()
    {
        var utterance = CreateRule(new IndividualTerm(new SemanticIdentifier("milk")));

        var created = UniversalRuleFactory.TryCreate(
            utterance.Meaning,
            utterance.SourceText,
            KnowledgeSourceKind.ImportedClaim,
            "test-pack",
            out var rule,
            out var descriptionFacts);

        Assert.True(created);
        Assert.Empty(descriptionFacts);
        Assert.Equal("friendly", rule.Conclusion.Predicate.Value);
        Assert.Equal("dog", Assert.Single(rule.Premises).Predicate.Value);
        Assert.Equal("cat", Assert.Single(rule.NegativePremises).Predicate.Value);
        Assert.Equal("likes", Assert.Single(rule.RelationalPremises).Predicate.Value);
        Assert.Equal(EvidencePolarity.Positive, rule.ConclusionPolarity);
        Assert.Equal(KnowledgeSourceKind.ImportedClaim, rule.SourceKind);
        Assert.Equal("test-pack", rule.SourceId);
    }

    [Fact]
    public void Rejects_a_relational_premise_with_a_variable_object()
    {
        var utterance = CreateRule(new VariableTerm(new SemanticIdentifier("y")));

        var created = UniversalRuleFactory.TryCreate(
            utterance.Meaning,
            utterance.SourceText,
            KnowledgeSourceKind.UserAssertion,
            null,
            out _,
            out _);

        Assert.False(created);
    }

    private static SemanticUtterance CreateRule(SemanticTerm relationObject)
    {
        var subject = new VariableTerm(new SemanticIdentifier("x"));
        var relationalPremise = new PredicateApplication(
            new SemanticIdentifier("likes"),
            ImmutableArray.Create<SemanticTerm>(subject, relationObject));
        var premises = new Conjunction(ImmutableArray.Create<SemanticFormula>(
            CreateUnaryPredicate("dog", subject),
            new Negation(CreateUnaryPredicate("cat", subject)),
            relationalPremise));
        var conclusion = CreateUnaryPredicate("friendly", subject);

        return new SemanticUtterance(
            UtteranceKind.Assertion,
            new UniversalQuantification(subject, new Implication(premises, conclusion)),
            "test universal rule");
    }

    private static PredicateApplication CreateUnaryPredicate(
        string predicate,
        SemanticTerm subject) =>
        new(
            new SemanticIdentifier(predicate),
            ImmutableArray.Create(subject));
}
