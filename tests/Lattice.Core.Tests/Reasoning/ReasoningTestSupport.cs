using Lattice.Core.Language;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using AwesomeAssertions;
using Xunit;

namespace Lattice.Core.Tests.Reasoning;

internal static class ReasoningTestSupport
{
    internal static ExistentialQuantification ParseExistentialQuestion(string sentence)
    {
        var utterance = Parse(sentence);
        utterance.Kind.Should().Be(UtteranceKind.Question);
        return utterance.Meaning.Should().BeOfType<ExistentialQuantification>().Which;
    }

    internal static KnowledgeEntryKind Add(
        KnowledgeBase knowledgeBase,
        string sentence)
    {
        var accepted = knowledgeBase.TryAdd(
            Parse(sentence),
            out var kind,
            out var error);

        accepted.Should().BeTrue(error);
        return kind;
    }

    internal static Negation ParseNegatedQuestion(string sentence)
    {
        var utterance = Parse(sentence);
        utterance.Kind.Should().Be(UtteranceKind.Question);
        return utterance.Meaning.Should().BeOfType<Negation>().Which;
    }

    internal static PredicateApplication ParseQuestion(string sentence)
    {
        var utterance = Parse(sentence);
        utterance.Kind.Should().Be(UtteranceKind.Question);
        return utterance.Meaning.Should().BeOfType<PredicateApplication>().Which;
    }

    internal static SemanticUtterance Parse(string sentence)
    {
        var result = new LceParser().Parse(sentence);
        result.IsSuccess.Should().BeTrue(result.Error);
        return result.Utterance.Should().BeOfType<SemanticUtterance>().Which;
    }
}
