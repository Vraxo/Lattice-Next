using System.Text.Json;

namespace Lattice.Client;

public sealed record LatticeCandidateAttributeEvidence(
    string Candidate,
    string AttributeName,
    JsonElement Value,
    string SourceText,
    string SourceUrl,
    DateTimeOffset RetrievedAt)
{
    public DateTimeOffset? FreshUntil { get; init; }
}
