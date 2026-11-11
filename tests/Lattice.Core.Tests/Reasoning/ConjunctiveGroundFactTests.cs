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
}
