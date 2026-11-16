namespace Lattice.Core.Generation;

public sealed record GenerationCandidateAssessment(
    string OperationId,
    bool IsEligible,
    string Reason,
    int PrerequisiteCount);
