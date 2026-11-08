using Lattice.Core.Context;
using Lattice.Core.Language;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Context;

public sealed class DefiniteDescriptionContextTests
{
    [Fact]
    public void Does_not_treat_words_inside_a_definite_description_as_individuals()
    {
        var parser = new LceParser();
        var context = new ConversationContext();
        var result = parser.Parse("Fido likes the red ball.");

        Assert.True(result.IsSuccess, result.Error);
        context.Observe(result.Utterance!);

        Assert.Equal(1, context.KnownIndividualCount);
    }
}
