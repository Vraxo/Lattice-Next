using Lattice.Core.Intelligence;
using Lattice.Core.Semantics;

namespace Lattice.Translator.Evaluation;

public sealed record EvaluationRecord(
    string CaseId,
    string Category,
    string Input,
    int Repetition,
    string ExpectedOutcome,
    string ObservedOutcome,
    IReadOnlyList<string> ExpectedMeanings,
    IReadOnlyList<string> ActualMeanings,
    string? Message,
    double LatencyMilliseconds,
    bool Passed,
    string? Detail)
{
    public static EvaluationRecord Create(
        BenchmarkCase item,
        int repetition,
        SemanticTranslationResult result,
        double latencyMilliseconds)
    {
        var actual = result.Candidates.Select(SemanticFormatter.Format).ToArray();
        var expectsMeaning = item.ExpectedMeaning is not null;
        var expected = expectsMeaning ? new[] { item.ExpectedMeaning! } : Array.Empty<string>();
        var observed = result.Status switch
        {
            SemanticTranslationStatus.ReadyForReview => "one-candidate",
            SemanticTranslationStatus.Ambiguous => "multiple-candidates",
            SemanticTranslationStatus.NoInterpretation => "no-candidate",
            SemanticTranslationStatus.Rejected => "rejected",
            SemanticTranslationStatus.Failed => "failed",
            _ => result.Status.ToString()
        };
        var passed = expectsMeaning
            ? actual.Length == 1 && string.Equals(actual[0], item.ExpectedMeaning, StringComparison.Ordinal)
            : actual.Length == 0 && result.Status == SemanticTranslationStatus.NoInterpretation;
        var detail = passed ? null : expectsMeaning
            ? $"Expected {item.ExpectedMeaning}; received [{string.Join("; ", actual)}]."
            : $"Expected a safe abstention; received {observed} with [{string.Join("; ", actual)}].";

        return new EvaluationRecord(
            item.Id, item.Category, item.Input, repetition, item.ExpectedOutcome,
            observed, expected, actual, result.Message, latencyMilliseconds, passed, detail);
    }
}
