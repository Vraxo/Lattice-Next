using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class AaaDiagnosticPackLoad
{
    [Fact]
    public void Reports_the_language_pack_loader_error()
    {
        var loader = new GenerationLanguagePackLoader();
        Assert.True(loader.TryLoadEmbedded(out var packs, out var error), error);
        Assert.Equal(2, packs.Count);
    }
}
