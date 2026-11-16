using System.Collections.Immutable;

namespace Lattice.Core.Generation;

public sealed record OperationSequenceGenerationResult(
    ImmutableArray<string> Tokens,
    string SelectedOperationId,
    ImmutableArray<GenerationCandidateAssessment> CandidateAssessments)
{
    public string Text => string.Concat(Tokens);
}
