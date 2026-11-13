namespace Lattice.Assistant.Benchmark;

public sealed record BenchmarkGroupSummary(
    string Name,
    int Runs,
    int HandledRuns,
    int AnsweredRuns,
    int SubstantiveAnswerRuns,
    int UnhandledRuns,
    double AverageLatencyMilliseconds);
