using Lattice.Core.Language;
using Lattice.Core.Reasoning;
using AwesomeAssertions;
using Xunit;
using static Lattice.Core.Tests.Reasoning.ReasoningTestSupport;

namespace Lattice.Core.Tests.Reasoning;

public sealed class ConjunctiveGroundFactTests
{
    [Fact]
    public void Stores_and_reasons_over_each_conjunct_of_a_named_individual_assertion()
    {
        var knowledgeBase = new KnowledgeBase();
        var utterance = Parse("Fido is happy and shiny.");

        var accepted = knowledgeBase.TryAdd(
            utterance,
            out var kind,
            out var error);

        accepted.Should().BeTrue(error);
        kind.Should().Be(KnowledgeEntryKind.Fact);
        knowledgeBase.Facts.Should().HaveCount(2);
        knowledgeBase.Facts.Should().OnlyContain(fact =>
            fact.SourceText == "Fido is happy and shiny.");

        var engine = new ReasoningEngine();
        var happy = engine.Ask(knowledgeBase, ParseQuestion("Is Fido happy?"));
        var shiny = engine.Ask(knowledgeBase, ParseQuestion("Is Fido shiny?"));

        happy.IsSupported.Should().BeTrue();
        happy.Proof!.SourceText.Should().Be("Fido is happy and shiny.");
        shiny.IsSupported.Should().BeTrue();
        shiny.Proof!.SourceText.Should().Be("Fido is happy and shiny.");
    }

    [Fact]
    public void Stores_and_reasons_over_class_membership_and_adjective_conjuncts()
    {
        var knowledgeBase = new KnowledgeBase();
        var utterance = Parse("Fido is a dog and happy.");

        var accepted = knowledgeBase.TryAdd(
            utterance,
            out var kind,
            out var error);

        accepted.Should().BeTrue(error);
        kind.Should().Be(KnowledgeEntryKind.Fact);
        knowledgeBase.Facts.Should().HaveCount(2);
        knowledgeBase.Facts.Should().OnlyContain(fact =>
            fact.SourceText == "Fido is a dog and happy.");

        var engine = new ReasoningEngine();
        var dog = engine.Ask(knowledgeBase, ParseQuestion("Does Fido count as a dog?"));
        var happy = engine.Ask(knowledgeBase, ParseQuestion("Is Fido happy?"));

        dog.IsSupported.Should().BeTrue();
        dog.Proof!.SourceText.Should().Be("Fido is a dog and happy.");
        happy.IsSupported.Should().BeTrue();
        happy.Proof!.SourceText.Should().Be("Fido is a dog and happy.");
    }

    [Fact]
    public void Stores_and_reasons_over_each_explicitly_negative_conjunct()
    {
        var knowledgeBase = new KnowledgeBase();
        var utterance = Parse("Fido is not happy and not shiny.");

        var accepted = knowledgeBase.TryAdd(
            utterance,
            out var kind,
            out var error);

        accepted.Should().BeTrue(error);
        kind.Should().Be(KnowledgeEntryKind.Fact);
        knowledgeBase.Facts.Should().HaveCount(2);
        knowledgeBase.Facts.Should().OnlyContain(fact =>
            fact.Polarity == EvidencePolarity.Negative &&
            fact.SourceText == "Fido is not happy and not shiny.");

        var engine = new ReasoningEngine();
        var unhappy = engine.Ask(knowledgeBase, ParseNegatedQuestion("Is Fido not happy?"));
        var notShiny = engine.Ask(knowledgeBase, ParseNegatedQuestion("Is Fido not shiny?"));

        unhappy.IsSupported.Should().BeTrue();
        unhappy.Proof!.SourceText.Should().Be("Fido is not happy and not shiny.");
        notShiny.IsSupported.Should().BeTrue();
        notShiny.Proof!.SourceText.Should().Be("Fido is not happy and not shiny.");
    }
}
