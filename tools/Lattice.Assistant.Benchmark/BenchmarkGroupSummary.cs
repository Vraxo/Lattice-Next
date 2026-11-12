namespace Lattice.Assistant.Benchmark;

public sealed record BenchmarkGroupSummary(
    string Name,
    int Runs,
    int HandledRuns,
    int UnhandledRuns,
    double AverageLatencyMilliseconds);
