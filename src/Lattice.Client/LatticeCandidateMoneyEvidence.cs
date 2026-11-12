namespace Lattice.Client;

/// <summary>A user-reviewed monetary observation about a known candidate.</summary>
public sealed record LatticeCandidateMoneyEvidence(
    string Candidate,
    decimal Amount,
    string CurrencySymbol,
    string SourceText,
    string SourceUrl,
    DateTimeOffset RetrievedAt);
