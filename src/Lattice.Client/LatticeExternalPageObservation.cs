namespace Lattice.Client;

/// <summary>A bounded public page retrieval result with its source metadata.</summary>
public sealed record LatticeExternalPageObservation(
    string SourceUrl,
    DateTimeOffset RetrievedAt,
    int HttpStatus,
    string ContentType,
    string Text,
    string? Title = null);
