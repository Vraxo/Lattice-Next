namespace Lattice.Assistant.Benchmark;

public sealed record BenchmarkRunReport(
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    string BenchmarkPath,
    string BenchmarkTitle,
    string BenchmarkStatus,
    string BenchmarkSha256,
    string ExecutionMode,
    int RepeatCount,
    int PromptCount,
    int ExecutedRuns,
    int HandledRuns,
    int AnsweredRuns,
    int UnhandledRuns,
    int RunnerErrorRuns,
    int StablePromptCount,
    double AverageLatencyMilliseconds,
    IReadOnlyList<BenchmarkGroupSummary> Categories,
    IReadOnlyList<BenchmarkGroupSummary> EvaluationLayers,
    IReadOnlyList<BenchmarkRunResult> Results);
