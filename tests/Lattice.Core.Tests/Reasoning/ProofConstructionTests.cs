using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using AwesomeAssertions;
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

        Add(knowledgeBase, "Every dog is an animal.").Should().Be(KnowledgeEntryKind.Rule);
        Add(knowledgeBase, "Fido is a dog.").Should().Be(KnowledgeEntryKind.Fact);

        var result = engine.Ask(knowledgeBase, ParseQuestion("Is Fido an animal?"));

        result.IsSupported.Should().BeTrue();

        var proof = result.Proof.Should().BeOfType<ProofNode>().Which;
        proof.Predicate.Predicate.Value.Should().Be("animal");
        proof.SourceText.Should().Be("Every dog is an animal.");

        var premise = proof.Premise.Should().BeOfType<ProofNode>().Which;
        premise.Predicate.Predicate.Value.Should().Be("dog");
        premise.SourceText.Should().Be("Fido is a dog.");
        premise.Premise.Should().BeNull();
    }

    [Fact]
    public void Retains_multiple_source_kinds_for_one_deduplicated_fact()
    {
        var knowledgeBase = new KnowledgeBase();
        var assertion = Parse("Fido is a dog.");

        knowledgeBase.TryAdd(assertion, out _, out var userError).Should().BeTrue(userError);
        knowledgeBase.TryAdd(
            assertion,
            out _,
            out var observationError,
            KnowledgeSourceKind.ExternalObservation).Should().BeTrue(observationError);

        var fact = knowledgeBase.Facts.Should().ContainSingle().Which;
        fact.Sources.Length.Should().Be(2);
        fact.Sources.Should().Contain(source =>
            source.Kind == KnowledgeSourceKind.UserAssertion);
        fact.Sources.Should().Contain(source =>
            source.Kind == KnowledgeSourceKind.ExternalObservation);

        var result = engine.Ask(knowledgeBase, ParseQuestion("Is Fido a dog?"));
        result.IsSupported.Should().BeTrue();
        result.Proof!.Sources.Length.Should().Be(2);
        result.Proof.Sources.Should().AllSatisfy(source =>
            source.SourceText.Should().Be("Fido is a dog."));
        result.Proof.Kind.Should().Be(ProofNodeKind.Asserted);
    }

    [Fact]
    public void Marks_rule_conclusions_as_derived_and_retains_rule_sources()
    {
        var knowledgeBase = new KnowledgeBase();
        Add(knowledgeBase, "Every dog is an animal.");
        Add(knowledgeBase, "Fido is a dog.");

        var result = engine.Ask(knowledgeBase, ParseQuestion("Is Fido an animal?"));

        result.IsSupported.Should().BeTrue();
        result.Proof!.Kind.Should().Be(ProofNodeKind.Derived);
        result.Proof.Sources[0].Kind.Should().Be(KnowledgeSourceKind.UserAssertion);
        result.Proof.Premise!.Kind.Should().Be(ProofNodeKind.Asserted);
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

        result.Proofs.Length.Should().Be(2);
        result.Proofs.Should().Contain(proof =>
            proof.SourceText == "Every dog is a pet." &&
            proof.Premise?.SourceText == "Fido is a dog.");
        result.Proofs.Should().Contain(proof =>
            proof.SourceText == "Every animal is a pet." &&
            proof.Premise?.SourceText == "Every dog is an animal.");
        result.Proofs.Should().AllSatisfy(proof => proof.Kind.Should().Be(ProofNodeKind.Derived));
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

        result.Proofs.Length.Should().Be(2);
        result.Proofs.Should().Contain(proof => proof.Kind == ProofNodeKind.Asserted);
        result.Proofs.Should().Contain(proof =>
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

        var animalProof = animal.Proofs.Should().ContainSingle().Which;
        var dogProof = dog.Proofs.Should().ContainSingle().Which;
        animalProof.SourceText.Should().Be("Every dog is an animal.");
        dogProof.Kind.Should().Be(ProofNodeKind.Asserted);
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

        result.Proofs.Length.Should().Be(2);
        result.Proofs.Should().Contain(proof =>
            proof.SourceText == "Every dog likes milk.");
        result.Proofs.Should().Contain(proof =>
            proof.SourceText == "Every animal likes milk." &&
            proof.Premise?.SourceText == "Every dog is an animal.");
    }
}
