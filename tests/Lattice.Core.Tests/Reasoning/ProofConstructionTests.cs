using System.Collections.Immutable;
using Lattice.Core.Language;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Reasoning;

public sealed class ProofConstructionTests
{
    private readonly ReasoningEngine engine = new();

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


    private static KnowledgeEntryKind Add(KnowledgeBase knowledgeBase, string sentence)
    {
        var accepted = knowledgeBase.TryAdd(Parse(sentence), out var kind, out var error);
        Assert.True(accepted, error);
        return kind;
    }

    private static PredicateApplication ParseQuestion(string sentence)
    {
        var utterance = Parse(sentence);
        Assert.Equal(UtteranceKind.Question, utterance.Kind);
        return Assert.IsType<PredicateApplication>(utterance.Meaning);
    }

    private static SemanticUtterance Parse(string sentence)
    {
        var result = new LceParser().Parse(sentence);
        Assert.True(result.IsSuccess, result.Error);
        return Assert.IsType<SemanticUtterance>(result.Utterance);
    }
}
