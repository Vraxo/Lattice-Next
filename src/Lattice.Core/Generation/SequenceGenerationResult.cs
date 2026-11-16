using System.Collections.Immutable;

namespace Lattice.Core.Generation;

public sealed record SequenceGenerationResult(
    ImmutableArray<string> Tokens,
    string SelectedOperationId,
    ImmutableArray<GenerationCandidateAssessment> CandidateAssessments)
{
    public string Text => string.Concat(Tokens);
}
