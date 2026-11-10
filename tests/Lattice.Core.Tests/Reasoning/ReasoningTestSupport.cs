using Lattice.Core.Language;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Reasoning;

internal static class ReasoningTestSupport
{
    internal static ExistentialQuantification ParseExistentialQuestion(string sentence)
    {
        var utterance = Parse(sentence);
        Assert.Equal(UtteranceKind.Question, utterance.Kind);
        return Assert.IsType<ExistentialQuantification>(utterance.Meaning);
    }

    internal static KnowledgeEntryKind Add(
        KnowledgeBase knowledgeBase,
        string sentence)
    {
        var accepted = knowledgeBase.TryAdd(
            Parse(sentence),
            out var kind,
            out var error);

        Assert.True(accepted, error);
        return kind;
    }

    internal static Negation ParseNegatedQuestion(string sentence)
    {
        var utterance = Parse(sentence);
        Assert.Equal(UtteranceKind.Question, utterance.Kind);
        return Assert.IsType<Negation>(utterance.Meaning);
    }

    internal static PredicateApplication ParseQuestion(string sentence)
    {
        var utterance = Parse(sentence);
        Assert.Equal(UtteranceKind.Question, utterance.Kind);
        return Assert.IsType<PredicateApplication>(utterance.Meaning);
    }

    internal static SemanticUtterance Parse(string sentence)
    {
        var result = new LceParser().Parse(sentence);
        Assert.True(result.IsSuccess, result.Error);
        return Assert.IsType<SemanticUtterance>(result.Utterance);
    }
}
