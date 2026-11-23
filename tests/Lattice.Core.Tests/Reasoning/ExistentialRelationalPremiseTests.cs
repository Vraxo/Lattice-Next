using Lattice.Core.Language;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Reasoning;

public sealed class ExistentialRelationalPremiseTests
{
    private readonly LceParser parser = new();
    private readonly ReasoningEngine engine = new();

    [Fact]
    public void Parses_a_relation_to_an_existentially_described_individual()
    {
        var result = parser.Parse("Every dog that chases a cat is friendly.");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(
            "forall(x, implies(and(dog(x), exists(y, and(cat(y), chases(x, y)))), friendly(x)))",
            SemanticFormatter.Format(result.Utterance!));
    }

    [Fact]
    public void Parses_a_fixed_object_relation_on_the_existential_witness()
    {
        var result = parser.Parse("Every dog that chases a cat that likes milk is friendly.");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(
            "forall(x, implies(and(dog(x), exists(y, and(cat(y), likes(y, milk), chases(x, y)))), friendly(x)))",
            SemanticFormatter.Format(result.Utterance!));
    }

    [Fact]
    public void Requires_witness_properties_to_match_the_same_related_individual()
    {
        var complete = new KnowledgeBase();
        Add(complete, "Every dog that chases a cat that likes milk is friendly.");
        Add(complete, "Fido is a dog.");
        Add(complete, "Fido chases Rover.");
        Add(complete, "Rover is a cat.");
        Add(complete, "Rover likes milk.");

        var result = engine.Ask(complete, ParseQuestion("Is Fido friendly?"));

        Assert.Equal(TruthValue.True, result.TruthValue);
        Assert.Contains(result.Proof!.AdditionalPremises, proof =>
            proof.SourceText == "Fido chases Rover.");
        Assert.Contains(result.Proof.AdditionalPremises, proof =>
            proof.SourceText == "Rover is a cat.");
        Assert.Contains(result.Proof.AdditionalPremises, proof =>
            proof.SourceText == "Rover likes milk.");

        var mismatchedWitness = new KnowledgeBase();
        Add(mismatchedWitness, "Every dog that chases a cat that likes milk is friendly.");
        Add(mismatchedWitness, "Fido is a dog.");
        Add(mismatchedWitness, "Fido chases Rover.");
        Add(mismatchedWitness, "Rover is a cat.");
        Add(mismatchedWitness, "Whiskers likes milk.");
        Assert.Equal(
            TruthValue.Unknown,
            engine.Ask(mismatchedWitness, ParseQuestion("Is Fido friendly?")).TruthValue);
    }

    [Fact]
    public void Parses_multiple_existential_relation_witnesses_in_one_description()
    {
        var result = parser.Parse(
            "Every dog that chases a cat and follows a person is friendly.");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(
            "forall(x, implies(and(dog(x), exists(y, and(cat(y), chases(x, y))), exists(z, and(person(z), follows(x, z)))), friendly(x)))",
            SemanticFormatter.Format(result.Utterance!));
    }

    [Fact]
    public void Requires_each_existential_relation_witness_to_match_its_own_conditions()
    {
        var complete = new KnowledgeBase();
        Add(complete, "Every dog that chases a cat and follows a person is friendly.");
        Add(complete, "Fido is a dog.");
        Add(complete, "Fido chases Rover.");
        Add(complete, "Rover is a cat.");
        Add(complete, "Fido follows Alice.");
        Add(complete, "Alice is a person.");

        var result = engine.Ask(complete, ParseQuestion("Is Fido friendly?"));

        Assert.Equal(TruthValue.True, result.TruthValue);
        Assert.Contains(result.Proof!.AdditionalPremises, proof =>
            proof.SourceText == "Fido chases Rover.");
        Assert.Contains(result.Proof.AdditionalPremises, proof =>
            proof.SourceText == "Rover is a cat.");
        Assert.Contains(result.Proof.AdditionalPremises, proof =>
            proof.SourceText == "Fido follows Alice.");
        Assert.Contains(result.Proof.AdditionalPremises, proof =>
            proof.SourceText == "Alice is a person.");

        var missingPerson = new KnowledgeBase();
        Add(missingPerson, "Every dog that chases a cat and follows a person is friendly.");
        Add(missingPerson, "Fido is a dog.");
        Add(missingPerson, "Fido chases Rover.");
        Add(missingPerson, "Rover is a cat.");
        Add(missingPerson, "Fido follows Alice.");
        Assert.Equal(
            TruthValue.Unknown,
            engine.Ask(missingPerson, ParseQuestion("Is Fido friendly?")).TruthValue);

        var wrongSubject = new KnowledgeBase();
        Add(wrongSubject, "Every dog that chases a cat and follows a person is friendly.");
        Add(wrongSubject, "Fido is a dog.");
        Add(wrongSubject, "Fido chases Rover.");
        Add(wrongSubject, "Rover is a cat.");
        Add(wrongSubject, "Rex follows Alice.");
        Add(wrongSubject, "Alice is a person.");
        Assert.Equal(
            TruthValue.Unknown,
            engine.Ask(wrongSubject, ParseQuestion("Is Fido friendly?")).TruthValue);
    }

    [Fact]
    public void Parses_nested_existential_relation_chains()
    {
        var result = parser.Parse(
            "Every dog that chases a cat that chases a mouse is friendly.");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(
            "forall(x, implies(and(dog(x), exists(y, and(cat(y), exists(z, and(mouse(z), chases(y, z))), chases(x, y)))), friendly(x)))",
            SemanticFormatter.Format(result.Utterance!));
    }

    [Fact]
    public void Requires_each_nested_relation_to_continue_from_the_parent_witness()
    {
        var complete = new KnowledgeBase();
        Add(complete, "Every dog that chases a cat that chases a mouse is friendly.");
        Add(complete, "Fido is a dog.");
        Add(complete, "Fido chases Rover.");
        Add(complete, "Rover is a cat.");
        Add(complete, "Rover chases Jerry.");
        Add(complete, "Jerry is a mouse.");

        var result = engine.Ask(complete, ParseQuestion("Is Fido friendly?"));

        Assert.Equal(TruthValue.True, result.TruthValue);
        Assert.Contains(result.Proof!.AdditionalPremises, proof =>
            proof.SourceText == "Fido chases Rover.");
        Assert.Contains(result.Proof.AdditionalPremises, proof =>
            proof.SourceText == "Rover is a cat.");
        Assert.Contains(result.Proof.AdditionalPremises, proof =>
            proof.SourceText == "Rover chases Jerry.");
        Assert.Contains(result.Proof.AdditionalPremises, proof =>
            proof.SourceText == "Jerry is a mouse.");

        var disconnectedChain = new KnowledgeBase();
        Add(disconnectedChain, "Every dog that chases a cat that chases a mouse is friendly.");
        Add(disconnectedChain, "Fido is a dog.");
        Add(disconnectedChain, "Fido chases Rover.");
        Add(disconnectedChain, "Rover is a cat.");
        Add(disconnectedChain, "Whiskers chases Jerry.");
        Add(disconnectedChain, "Jerry is a mouse.");
        Assert.Equal(
            TruthValue.Unknown,
            engine.Ask(disconnectedChain, ParseQuestion("Is Fido friendly?")).TruthValue);
    }

    [Fact]
    public void Rejects_negated_existential_relation_conditions()
    {
        var result = parser.Parse("Every dog that does not chase a cat is friendly.");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Utterance);

        var nestedNegation = parser.Parse(
            "Every dog that chases a cat that does not like milk is friendly.");
        Assert.False(nestedNegation.IsSuccess);
        Assert.Null(nestedNegation.Utterance);
    }

    [Fact]
    public void Derives_a_conclusion_only_when_one_related_individual_satisfies_the_witness_conditions()
    {
        var complete = new KnowledgeBase();
        Add(complete, "Every dog that chases a cat is friendly.");
        Add(complete, "Fido is a dog.");
        Add(complete, "Fido chases Whiskers.");
        Add(complete, "Fido chases Rover.");
        Add(complete, "Whiskers is a mouse.");
        Add(complete, "Rover is a cat.");

        var result = engine.Ask(complete, ParseQuestion("Is Fido friendly?"));

        Assert.Equal(TruthValue.True, result.TruthValue);
        Assert.Contains(result.Proof!.AdditionalPremises, proof =>
            proof.SourceText == "Fido chases Rover.");
        Assert.Contains(result.Proof.AdditionalPremises, proof =>
            proof.SourceText == "Rover is a cat.");
        Assert.DoesNotContain(result.Proof.AdditionalPremises, proof =>
            proof.SourceText == "Fido chases Whiskers.");

        var missingWitnessType = new KnowledgeBase();
        Add(missingWitnessType, "Every dog that chases a cat is friendly.");
        Add(missingWitnessType, "Fido is a dog.");
        Add(missingWitnessType, "Fido chases Whiskers.");
        Assert.Equal(
            TruthValue.Unknown,
            engine.Ask(missingWitnessType, ParseQuestion("Is Fido friendly?")).TruthValue);

        var wrongSubject = new KnowledgeBase();
        Add(wrongSubject, "Every dog that chases a cat is friendly.");
        Add(wrongSubject, "Fido is a dog.");
        Add(wrongSubject, "Rex chases Whiskers.");
        Add(wrongSubject, "Whiskers is a cat.");
        Assert.Equal(
            TruthValue.Unknown,
            engine.Ask(wrongSubject, ParseQuestion("Is Fido friendly?")).TruthValue);
    }

    private PredicateApplication ParseQuestion(string sentence)
    {
        var result = parser.Parse(sentence);
        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.IsType<SemanticUtterance>(result.Utterance);
        return Assert.IsType<PredicateApplication>(utterance.Meaning);
    }

    private void Add(KnowledgeBase knowledgeBase, string sentence)
    {
        var parsed = parser.Parse(sentence);
        Assert.True(parsed.IsSuccess, parsed.Error);
        var accepted = knowledgeBase.TryAdd(
            parsed.Utterance!,
            out _,
            out var error);
        Assert.True(accepted, error);
    }
}
