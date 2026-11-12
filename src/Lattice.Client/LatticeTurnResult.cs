using System.Collections.Immutable;

namespace Lattice.Client;

public enum LatticeTurnStatus
{
    Completed,
    Rejected
}

public sealed record LatticeTurnResult(
    LatticeTurnStatus Status,
    ImmutableArray<LatticeUtteranceResult> Utterances,
    string? Error = null)
{
    public bool IsSuccess => Status == LatticeTurnStatus.Completed;

    public static LatticeTurnResult Completed(
        ImmutableArray<LatticeUtteranceResult> utterances) =>
        new(LatticeTurnStatus.Completed, utterances);

    public static LatticeTurnResult Rejected(string error) =>
        new(LatticeTurnStatus.Rejected, ImmutableArray<LatticeUtteranceResult>.Empty, error);
}

public enum LatticeUtteranceResultKind
{
    StatementStored,
    StatementRejected,
    QuestionAnswered,
    ClarificationRequired,
    ObjectiveReceived,
    ObjectiveInformationReceived,
    InformationUnavailable,
    UnsupportedQuestion
}

public enum LatticeKnowledgeEntryKind
{
    Fact,
    Rule
}

public enum LatticeTruthValue
{
    True,
    False,
    Both,
    Unknown
}

public enum LatticeReferenceStatus
{
    MissingReferent,
    AmbiguousReferent
}

public sealed record LatticeUtteranceResult(
    LatticeUtteranceResultKind Kind,
    string? Message = null,
    string? Meaning = null,
    LatticeKnowledgeEntryKind? EntryKind = null,
    LatticeTruthValue? TruthValue = null,
    LatticeReferenceStatus? ReferenceStatus = null,
    ImmutableArray<LatticeEvidence> SupportingEvidence = default,
    ImmutableArray<LatticeEvidence> OpposingEvidence = default);

public enum LatticeEvidenceKind
{
    Asserted,
    Derived
}

public enum LatticeEvidencePolarity
{
    Positive,
    Negative
}

public enum LatticeKnowledgeSourceKind
{
    UserAssertion,
    ImportedClaim,
    ExternalObservation
}

public sealed record LatticeEvidence(
    LatticeEvidenceKind Kind,
    LatticeEvidencePolarity Polarity,
    ImmutableArray<LatticeEvidenceSource> Sources,
    ImmutableArray<LatticeEvidence> Premises);

public sealed record LatticeEvidenceSource(
    LatticeKnowledgeSourceKind Kind,
    string SourceText,
    string? SourceId,
    string? ResolvedReferent);
