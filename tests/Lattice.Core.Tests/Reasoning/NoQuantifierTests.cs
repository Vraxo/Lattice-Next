using Lattice.Core.Language;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Reasoning;

public sealed class NoQuantifierTests
{
    private readonly LceParser parser = new();
    private readonly ReasoningEngine engine = new();

    [Fact]
    public void No_quantifier_derives_negative_class_membership_for_singular_and_plural_forms()
    {
        var plural = new KnowledgeBase();
        Add(plural, "No dogs are cats.");
        Add(plural, "Fido is a dog.");

        var pluralResult = engine.Ask(plural, ParseQuestion("Is Fido a cat?"));

        Assert.Equal(TruthValue.False, pluralResult.TruthValue);
        var pluralProof = Assert.Single(pluralResult.ContradictingProofs);
        Assert.Equal("No dogs are cats.", pluralProof.SourceText);
        Assert.Equal("Fido is a dog.", pluralProof.Premise!.SourceText);

        var singular = new KnowledgeBase();
        Add(singular, "No dog is a cat.");
        Add(singular, "Fido is a dog.");

        var singularResult = engine.Ask(singular, ParseQuestion("Is Fido a cat?"));

        Assert.Equal(TruthValue.False, singularResult.TruthValue);
        Assert.Equal("No dog is a cat.", Assert.Single(singularResult.ContradictingProofs).SourceText);
    }

    [Fact]
    public void No_quantifier_derives_negative_fixed_object_relations()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "No dogs like milk.");
        Add(knowledgeBase, "Fido is a dog.");

        var result = engine.Ask(knowledgeBase, ParseQuestion("Does Fido like milk?"));

        Assert.Equal(TruthValue.False, result.TruthValue);
        var proof = Assert.Single(result.ContradictingProofs);
        Assert.Equal("No dogs like milk.", proof.SourceText);
        Assert.Equal("Fido is a dog.", proof.Premise!.SourceText);
    }

    [Fact]
    public void No_quantifier_composes_with_explicit_negative_relational_premises()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "No dogs that do not like milk are friendly.");
        Add(knowledgeBase, "Fido is a dog.");
        Add(knowledgeBase, "Fido does not like milk.");

        var result = engine.Ask(knowledgeBase, ParseQuestion("Is Fido friendly?"));

        Assert.Equal(TruthValue.False, result.TruthValue);
        var proof = Assert.Single(result.ContradictingProofs);
        Assert.Equal("No dogs that do not like milk are friendly.", proof.SourceText);
        Assert.Equal("Fido is a dog.", proof.Premise!.SourceText);
        var relation = Assert.Single(proof.AdditionalPremises);
        Assert.Equal("Fido does not like milk.", relation.SourceText);
        Assert.Equal(EvidencePolarity.Negative, relation.Polarity);
    }

    private void Add(KnowledgeBase knowledgeBase, string sentence)
    {
        var parsed = parser.Parse(sentence);
        Assert.True(parsed.IsSuccess, parsed.Error);
        Assert.True(knowledgeBase.TryAdd(parsed.Utterance!, out _, out var error), error);
    }

    private PredicateApplication ParseQuestion(string sentence)
    {
        var parsed = parser.Parse(sentence);
        Assert.True(parsed.IsSuccess, parsed.Error);
        return Assert.IsType<PredicateApplication>(parsed.Utterance!.Meaning);
    }
}
