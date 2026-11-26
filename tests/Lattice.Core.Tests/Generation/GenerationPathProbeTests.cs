using System.Collections.Immutable;
using System.Linq;
using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

// Temporary probe: reports which stage of the knowledge generation path fails for a request.
public sealed class GenerationPathProbeTests
{
    [Theory]
    [InlineData("write a hello world program in python")]
    [InlineData("Write a Hello World program in Python.")]
    [InlineData("Create a Python program that prints \"Hello, world!\".")]
    public void Probe(string input)
    {
        Assert.True(
            SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var knowledgeError),
            $"knowledge: {knowledgeError}");

        Assert.True(
            SequenceInputFactExtractor.TryExtract(knowledge!, input, out var facts, out var extractError),
            $"extract: {extractError}");

        var ids = string.Join(", ", facts.Select(fact => fact.Id));
        Assert.Contains(facts, fact => fact.Id == "intent.program-generation");

        var loader = new GenerationLanguagePackLoader();
        Assert.True(loader.TryLoadEmbedded(out var packs, out var packError), $"packs: {packError}");
        Assert.True(packs.TryGetValue("python", out var pack), "python pack missing");

        Assert.True(
            SemanticProgramPlanner.TryGenerateProgram(facts, pack!, out var code, out _, out var planError),
            $"plan: {planError} | facts: {ids}");

        var request = new KnowledgeDrivenSequenceGenerationRequest(
            ImmutableArray<string>.Empty,
            facts.Select(fact => fact.Id).ToImmutableArray())
        {
            FactValues = facts,
            LanguagePack = pack
        };

        Assert.True(
            KnowledgeDrivenSequenceGenerator.TryGenerate(
                knowledge!, null, request, out var generated, out var sequenceError),
            $"sequence: {sequenceError} | facts: {ids} | code: {code}");

        Assert.False(string.IsNullOrWhiteSpace(generated!.Text));
    }
}
