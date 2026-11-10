using Lattice.Core.Language;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Reasoning;

public sealed class KnowledgeFactFactoryTests
{
    [Fact]
    public void Creates_grounded_facts_and_description_facts_for_a_descriptive_assertion()
    {
        var utterance = Parse("The red ball is shiny.");

        var created = KnowledgeFactFactory.TryCreateGroundFacts(
            utterance.Meaning,
            utterance.SourceText,
            utterance.ResolvedReferent,
            KnowledgeSourceKind.UserAssertion,
            null,
            out var facts);

        Assert.True(created);
        Assert.Equal(3, facts.Length);
        Assert.Equal("shiny", facts[0].Predicate.Predicate.Value);
        Assert.Equal("ball", facts[1].Predicate.Predicate.Value);
        Assert.Equal("red", facts[2].Predicate.Predicate.Value);
        Assert.Equal(
            facts[0].Predicate.Arguments[0],
            facts[1].Predicate.Arguments[0]);
        Assert.Equal(
            facts[0].Predicate.Arguments[0],
            facts[2].Predicate.Arguments[0]);
    }

    [Fact]
    public void Preserves_explicit_negative_polarity_when_creating_ground_facts()
    {
        var utterance = Parse("Fido does not like milk.");

        var created = KnowledgeFactFactory.TryCreateGroundFacts(
            utterance.Meaning,
            utterance.SourceText,
            utterance.ResolvedReferent,
            KnowledgeSourceKind.UserAssertion,
            null,
            out var facts);

        Assert.True(created);
        var fact = Assert.Single(facts);
        Assert.Equal(EvidencePolarity.Negative, fact.Polarity);
    }

    [Fact]
    public void Attaches_every_existential_condition_to_the_supplied_witness()
    {
        var utterance = Parse("A red ball is shiny.");
        Assert.IsType<ExistentialQuantification>(utterance.Meaning);
        var existential = (ExistentialQuantification)utterance.Meaning;

        Assert.True(KnowledgeFactFactory.TryGetBoundUnaryPredicates(
            existential.Body,
            existential.Variable,
            out var conditions));

        var witness = new AnonymousIndividualTerm(new SemanticIdentifier("test-witness"));
        var facts = KnowledgeFactFactory.CreateExistentialFacts(
            conditions,
            witness,
            utterance.SourceText,
            KnowledgeSourceKind.ImportedClaim,
            "test-pack");

        Assert.Equal(conditions.Length, facts.Length);
        Assert.All(facts, fact =>
        {
            Assert.Equal(witness, Assert.Single(fact.Predicate.Arguments));
            Assert.Equal(KnowledgeSourceKind.ImportedClaim, fact.SourceKind);
            Assert.Equal("test-pack", fact.SourceId);
        });
    }

    private static SemanticUtterance Parse(string sentence)
    {
        var result = new LceParser().Parse(sentence);
        Assert.True(result.IsSuccess, result.Error);
        return result.Utterance!;
    }
}
