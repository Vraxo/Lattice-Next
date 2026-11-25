using System.Collections.Immutable;
using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests;

public sealed class AaaDiagnosticsTests
{
    [Fact]
    public void Reports_the_hello_world_pipeline_state()
    {
        const string prompt = "Create a Python program that prints \"Hello, world!\".";

        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var knowledgeError), knowledgeError);
        Assert.True(new GenerationLanguagePackLoader().TryLoadEmbedded(out var packs, out var packError), packError);
        Assert.True(packs.TryGetValue("python", out var pack), "no python pack");

        var extracted = SequenceInputFactExtractor.TryExtract(knowledge, prompt, out var facts, out var extractError);
        var requestedFacts = extracted ? facts.Select(fact => fact.Id).ToImmutableArray() : ImmutableArray<string>.Empty;
        var request = new KnowledgeDrivenSequenceGenerationRequest(ImmutableArray<string>.Empty, requestedFacts)
        {
            FactValues = extracted ? facts : ImmutableArray<SequenceSemanticFact>.Empty,
            LanguagePack = pack
        };
        var generated = SequenceKnowledgeDrivenState(
            knowledge,
            request,
            out var generateError,
            out var text);
        var validated = extracted &&
            SequenceInputFactValidator.TryValidate(knowledge.Validations, facts, pack, out var validateError) &&
            validateError is null;
        var validationDetail = extracted &&
            SequenceInputFactValidator.TryValidate(knowledge.Validations, facts, pack, out var detail)
            ? "none"
            : "failed";

        Assert.True(
            false,
            $"DIAG extract={extracted} extractError={extractError} "
                + $"facts={string.Join(",", requestedFacts)} "
                + $"generate={generated} generateError={generateError} "
                + $"text={text} validate={validated} validationDetail={validationDetail}");
    }

    private static bool SequenceKnowledgeDrivenState(
        SequenceKnowledgeBase knowledge,
        KnowledgeDrivenSequenceGenerationRequest request,
        out string? error,
        out string text)
    {
        var ok = KnowledgeDrivenSequenceGenerator.TryGenerate(knowledge, null, request, out var result, out error);
        text = result?.Text is { } value ? value.Replace("\n", "\\n", StringComparison.Ordinal) : "none";
        return ok;
    }
}
