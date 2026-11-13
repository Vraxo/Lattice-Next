namespace Lattice.Assistant.Benchmark;

public sealed record BenchmarkRunResult(
    string Id,
    string Prompt,
    string Category,
    string EvaluationLayer,
    bool SubstantiveAnswerReviewed,
    int Repetition,
    int ExitCode,
    bool CliSuccess,
    string RunStatus,
    string? TurnStatus,
    IReadOnlyList<string> UtteranceKinds,
    string ResponseSummary,
    string? Error,
    string? OutcomeSignature,
    double LatencyMilliseconds,
    string StandardOutput,
    string StandardError);
