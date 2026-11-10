using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using Xunit;
using static Lattice.Core.Tests.Reasoning.ReasoningTestSupport;

namespace Lattice.Core.Tests.Reasoning;

public sealed class ProofConstructionTests
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
    public void Retains_multiple_source_kinds_for_one_deduplicated_fact()
    {
        var knowledgeBase = new KnowledgeBase();
        var assertion = Parse("Fido is a dog.");

        Assert.True(knowledgeBase.TryAdd(assertion, out _, out var userError), userError);
        Assert.True(knowledgeBase.TryAdd(
            assertion,
            out _,
            out var observationError,
            KnowledgeSourceKind.ExternalObservation), observationError);

        var fact = Assert.Single(knowledgeBase.Facts);
        Assert.Equal(2, fact.Sources.Length);
        Assert.Contains(fact.Sources, source =>
            source.Kind == KnowledgeSourceKind.UserAssertion);
        Assert.Contains(fact.Sources, source =>
            source.Kind == KnowledgeSourceKind.ExternalObservation);

        var result = engine.Ask(knowledgeBase, ParseQuestion("Is Fido a dog?"));
        Assert.True(result.IsSupported);
        Assert.Equal(2, result.Proof!.Sources.Length);
        Assert.All(result.Proof.Sources, source =>
            Assert.Equal("Fido is a dog.", source.SourceText));
        Assert.Equal(ProofNodeKind.Asserted, result.Proof.Kind);
    }

    [Fact]
    public void Marks_rule_conclusions_as_derived_and_retains_rule_sources()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog is an animal.");
        Add(knowledgeBase, "Fido is a dog.");

        var result = engine.Ask(knowledgeBase, ParseQuestion("Is Fido an animal?"));

        Assert.True(result.IsSupported);
        Assert.Equal(ProofNodeKind.Derived, result.Proof!.Kind);
        Assert.Equal(KnowledgeSourceKind.UserAssertion, result.Proof.Sources[0].Kind);
        Assert.Equal(ProofNodeKind.Asserted, result.Proof.Premise!.Kind);
    }

    [Fact]
    public void Retains_independent_rule_paths_for_the_same_conclusion()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog is a pet.");
        Add(knowledgeBase, "Every animal is a pet.");
        Add(knowledgeBase, "Every dog is an animal.");
        Add(knowledgeBase, "Fido is a dog.");

        var result = engine.Ask(
            knowledgeBase,
            ParseQuestion("Is Fido a pet?"));

        Assert.Equal(2, result.Proofs.Length);
        Assert.Contains(result.Proofs, proof =>
            proof.SourceText == "Every dog is a pet." &&
            proof.Premise?.SourceText == "Fido is a dog.");
        Assert.Contains(result.Proofs, proof =>
            proof.SourceText == "Every animal is a pet." &&
            proof.Premise?.SourceText == "Every dog is an animal.");
        Assert.All(result.Proofs, proof => Assert.Equal(ProofNodeKind.Derived, proof.Kind));
    }

    [Fact]
    public void Retains_both_direct_and_derived_support_for_one_fact()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog is an animal.");
        Add(knowledgeBase, "Fido is a dog.");
        Add(knowledgeBase, "Fido is an animal.");

        var result = engine.Ask(
            knowledgeBase,
            ParseQuestion("Is Fido an animal?"));

        Assert.Equal(2, result.Proofs.Length);
        Assert.Contains(result.Proofs, proof => proof.Kind == ProofNodeKind.Asserted);
        Assert.Contains(result.Proofs, proof =>
            proof.Kind == ProofNodeKind.Derived &&
            proof.SourceText == "Every dog is an animal.");
    }

    [Fact]
    public void Does_not_create_cyclic_proofs_when_rules_refer_to_each_other()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog is an animal.");
        Add(knowledgeBase, "Every animal is a dog.");
        Add(knowledgeBase, "Fido is a dog.");

        var animal = engine.Ask(
            knowledgeBase,
            ParseQuestion("Is Fido an animal?"));
        var dog = engine.Ask(
            knowledgeBase,
            ParseQuestion("Is Fido a dog?"));

        var animalProof = Assert.Single(animal.Proofs);
        var dogProof = Assert.Single(dog.Proofs);
        Assert.Equal("Every dog is an animal.", animalProof.SourceText);
        Assert.Equal(ProofNodeKind.Asserted, dogProof.Kind);
    }

    [Fact]
    public void Retains_independent_relational_rule_paths()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog likes milk.");
        Add(knowledgeBase, "Every animal likes milk.");
        Add(knowledgeBase, "Every dog is an animal.");
        Add(knowledgeBase, "Fido is a dog.");

        var result = engine.Ask(
            knowledgeBase,
            ParseQuestion("Does Fido like milk?"));

        Assert.Equal(2, result.Proofs.Length);
        Assert.Contains(result.Proofs, proof =>
            proof.SourceText == "Every dog likes milk.");
        Assert.Contains(result.Proofs, proof =>
            proof.SourceText == "Every animal likes milk." &&
            proof.Premise?.SourceText == "Every dog is an animal.");
    }
}
