using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests;

public sealed class AaaDiagnosticsTests
{
    [Fact]
    public void Loads_embedded_knowledge()
    {
        var knowledgeOk = SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var knowledgeError);
        var packOk = new GenerationLanguagePackLoader().TryLoadEmbedded(out var packs, out var packError);
        Assert.True(
            knowledgeOk && packOk,
            $"knowledge={knowledgeOk} ({knowledgeError}) packs={packOk} ({packError}) rules={knowledge?.Rules.Length} inputRules={knowledge?.InputRules.Length}");
    }
}
