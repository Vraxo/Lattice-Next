using System.Text.Json.Serialization;

namespace Lattice.Assistant.Benchmark;

internal sealed record BenchmarkRecord(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("prompt")] string Prompt,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("evaluation_layer")] string EvaluationLayer,
    [property: JsonPropertyName("substantive_answer_reviewed")] bool SubstantiveAnswerReviewed = false);
