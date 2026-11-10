using Lattice.Core.Language;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Reasoning;

public sealed class UniversalRuleFactoryTests
{
    [Fact]
    public void Creates_rules_with_separate_positive_negative_and_relational_premises()
    {
        var utterance = Parse(
            "forall(x, implies(and(dog(x), not(cat(x)), likes(x, milk)), friendly(x)))");

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
        var utterance = Parse(
            "forall(x, implies(and(dog(x), likes(x, y)), friendly(x)))");

        var created = UniversalRuleFactory.TryCreate(
            utterance.Meaning,
            utterance.SourceText,
            KnowledgeSourceKind.UserAssertion,
            null,
            out _,
            out _);

        Assert.False(created);
    }

    private static SemanticUtterance Parse(string sentence)
    {
        var result = new LceParser().Parse(sentence);
        Assert.True(result.IsSuccess, result.Error);
        return result.Utterance!;
    }
}
