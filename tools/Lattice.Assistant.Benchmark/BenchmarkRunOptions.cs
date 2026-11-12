namespace Lattice.Assistant.Benchmark;

public sealed record BenchmarkRunOptions(
    string BenchmarkPath,
    string JsonOutputPath,
    string MarkdownOutputPath,
    int Repeat = 1,
    string? Category = null,
    string? EvaluationLayer = null,
    IReadOnlySet<string>? Ids = null);
