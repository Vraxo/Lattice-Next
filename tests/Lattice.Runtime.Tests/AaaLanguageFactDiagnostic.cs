using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Runtime.Tests;

public sealed class AaaLanguageFactDiagnostic
{
    [Fact]
    public void Shows_the_language_facts_of_a_python_request()
    {
        var loader = new GenerationLanguagePackLoader();
        Assert.True(loader.TryLoadEmbedded(out var packs, out var packError), packError);

        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        Assert.True(
            SequenceInputFactExtractor.TryExtract(
                knowledge!,
                "Create a Python program that sets total to 7 then print total.",
                out var facts,
                out var extractionError),
            extractionError);

        var ids = string.Join(", ", facts.Select(fact => fact.Id));
        Assert.True(packs.Count == 42, $"DIAGNOSTIC packs={packs.Count} keys={string.Join('|', packs.Keys)} facts={ids}");
    }
}
