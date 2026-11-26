using System.Collections.Immutable;
using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

/// <summary>
/// The cross-language proof: one request is interpreted once, planned onto the same operations, and realized by
/// whichever language pack is selected. Only the pack differs.
/// </summary>
public sealed class CrossLanguageGenerationTests
{
    private const string ScalarRequest =
        "Create a Python program that sets total to 7 then print total.";

    private const string ScalarPython = "total = 7\nprint(total)";

    private const string ScalarJavaScript = "let total = 7\nconsole.log(total)";

    private const string FilterRequest =
        "Build a Python function named keep_large that accepts a list of numbers named samples and returns a new list containing only sample values more than 4.";

    private const string FilterPython =
        "def keep_large(samples):\n    return [sample for sample in samples if sample > 4]\nprint(keep_large([1, 2, 3, 4, 5]))";

    private const string FilterJavaScript =
        "function keep_large(samples) {\n  return samples.filter((sample) => sample > 4)\n}\nconsole.log(keep_large([1, 2, 3, 4, 5]))";

    private const string DictionaryRequest =
        "Write a Python program that fulfills this request: Given a dictionary of product names and prices, print each product with its price.";

    private const string DictionaryPython =
        "items = {}  # Add dictionary entries here.\nfor product, price in items.items():\n    print(product, price)";

    private const string DictionaryJavaScript =
        "let items = {};  // Add dictionary entries here.\nfor (const [product, price] of Object.entries(items)) {\n  console.log(product, price)\n}";

    [Theory]
    [InlineData(ScalarRequest, ScalarPython, ScalarJavaScript)]
    [InlineData(FilterRequest, FilterPython, FilterJavaScript)]
    [InlineData(DictionaryRequest, DictionaryPython, DictionaryJavaScript)]
    public void Realizes_the_same_interpretation_in_each_language(
        string request,
        string expectedPython,
        string expectedJavaScript)
    {
        var packs = LoadPacks();
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        Assert.True(
            SequenceInputFactExtractor.TryExtract(knowledge!, request, out var facts, out var extractionError),
            extractionError);

        Assert.True(
            SemanticProgramPlanner.TryGenerateProgram(
                facts, packs["python"], out var pythonCode, out var pythonStatements, out var pythonError),
            pythonError);
        Assert.True(
            SemanticProgramPlanner.TryGenerateProgram(
                facts, packs["javascript"], out var javaScriptCode, out var javaScriptStatements, out var javaScriptError),
            javaScriptError);

        Assert.Equal(expectedPython, pythonCode);
        Assert.Equal(expectedJavaScript, javaScriptCode);

        // The operations are selected language-neutrally: the same effects, in the same order, either way.
        Assert.Equal(
            pythonStatements.Select(statement => statement.Effect).ToImmutableArray(),
            javaScriptStatements.Select(statement => statement.Effect).ToImmutableArray());
    }

    [Theory]
    [InlineData(ScalarRequest, ScalarJavaScript)]
    [InlineData(FilterRequest, FilterJavaScript)]
    [InlineData(DictionaryRequest, DictionaryJavaScript)]
    public void Selects_the_requested_language_pack(string request, string expectedJavaScript)
    {
        var packs = LoadPacks();
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);

        var javaScriptRequest = request.Replace("Python", "JavaScript", StringComparison.Ordinal);
        Assert.True(
            SequenceInputFactExtractor.TryExtract(
                knowledge!, javaScriptRequest, out var facts, out var extractionError),
            extractionError);

        var language = Assert.Single(
            facts.Where(fact => fact.Id.StartsWith("language.", StringComparison.Ordinal)));
        Assert.Equal("language.javascript", language.Id);

        Assert.True(
            SemanticProgramPlanner.TryGenerateProgram(
                facts, packs["javascript"], out var code, out _, out var error),
            error);
        Assert.Equal(expectedJavaScript, code);
    }

    private static ImmutableDictionary<string, GenerationLanguagePack> LoadPacks()
    {
        var loader = new GenerationLanguagePackLoader();
        Assert.True(loader.TryLoadEmbedded(out var packs, out var error), error);
        Assert.Contains("python", packs.Keys);
        Assert.Contains("javascript", packs.Keys);
        return packs;
    }
}
