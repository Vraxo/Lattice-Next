using System.Text.Json.Serialization;

namespace Lattice.Translator.Evaluation;

public sealed record BenchmarkDocument
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; }
    [JsonPropertyName("translationDirection")]
    public string TranslationDirection { get; init; } = string.Empty;
    [JsonPropertyName("cases")]
    public List<BenchmarkCase> Cases { get; init; } = [];
}

public sealed record BenchmarkCase
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;
    [JsonPropertyName("category")]
    public string Category { get; init; } = string.Empty;
    [JsonPropertyName("input")]
    public string Input { get; init; } = string.Empty;
    [JsonPropertyName("expectedOutcome")]
    public string ExpectedOutcome { get; init; } = string.Empty;
    [JsonPropertyName("expectedMeaning")]
    public string? ExpectedMeaning { get; init; }
    [JsonPropertyName("notes")]
    public string? Notes { get; init; }
}
