using System.Text.Json.Serialization;

namespace Lattice.Assistant.Benchmark;

internal sealed record BenchmarkDocument(
    [property: JsonPropertyName("schema_version")] int SchemaVersion,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("records")] IReadOnlyList<BenchmarkRecord> Records);
