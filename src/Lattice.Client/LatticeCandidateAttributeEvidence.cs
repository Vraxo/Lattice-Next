using Lattice.Core.Semantics;

namespace Lattice.Client;

public sealed record LatticeCandidateAttributeEvidence(
    string Candidate,
    string AttributeName,
    ObjectiveConditionValue Value,
    string SourceText,
    string SourceUrl,
    DateTimeOffset RetrievedAt)
{
    public DateTimeOffset? FreshUntil { get; init; }
}
