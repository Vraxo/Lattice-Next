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

    private SemanticUtterance ParseQuestion(string sentence)
    {
        var result = parser.Parse(sentence);
        Assert.True(result.IsSuccess, result.Error);
        return Assert.IsType<SemanticUtterance>(result.Utterance);
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
