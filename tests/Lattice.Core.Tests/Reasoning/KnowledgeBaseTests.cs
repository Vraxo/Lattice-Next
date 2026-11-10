using System.Collections.Generic;
using Lattice.Core.Language;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Reasoning;

public sealed class KnowledgeBaseTests
{
    [Fact]
    public void Facts_are_exposed_through_a_live_read_only_collection()
    {
        var knowledgeBase = new KnowledgeBase();
        var facts = Assert.IsAssignableFrom<IList<KnowledgeFact>>(knowledgeBase.Facts);

        Assert.True(facts.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => facts.Clear());

        Add(knowledgeBase, "Fido is a dog.");

        Assert.Single(facts);
        Assert.Equal("dog", facts[0].Predicate.Predicate.Value);
    }

    [Fact]
    public void Rules_are_exposed_through_a_live_read_only_collection()
    {
        var knowledgeBase = new KnowledgeBase();
        var rules = Assert.IsAssignableFrom<IList<UniversalRule>>(knowledgeBase.Rules);

        Assert.True(rules.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => rules.Clear());

        Add(knowledgeBase, "Every dog is an animal.");

        Assert.Single(rules);
        Assert.Equal("animal", rules[0].Conclusion.Predicate.Value);
    }

    private static void Add(KnowledgeBase knowledgeBase, string sentence)
    {
        var parsed = new LceParser().Parse(sentence);
        Assert.True(parsed.IsSuccess, parsed.Error);
        Assert.NotNull(parsed.Utterance);

        var accepted = knowledgeBase.TryAdd(
            parsed.Utterance,
            out _,
            out var error);

        Assert.True(accepted, error);
    }
}
