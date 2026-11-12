using System.Text.RegularExpressions;
using System.Text.Json;

namespace Lattice.Client;

public static class LatticeReviewedCandidateAttributeEvidenceFactory
{
    private static readonly Regex AttributeNamePattern = new(
        "^[a-z][a-z0-9-]{0,63}$",
        RegexOptions.CultureInvariant);

    public static bool TryCreate(
        LatticeExternalPageObservation page,
        string? candidate,
        string? attributeName,
        JsonElement value,
        string? excerpt,
        out LatticeCandidateAttributeEvidence? evidence,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(page);

        evidence = null;
        error = null;

        if (string.IsNullOrWhiteSpace(candidate) ||
            candidate.Length > 256 ||
            string.IsNullOrWhiteSpace(attributeName) ||
            !AttributeNamePattern.IsMatch(attributeName) ||
            value.ValueKind != JsonValueKind.Object ||
            string.IsNullOrWhiteSpace(excerpt) ||
            excerpt.Length > 4000)
        {
            error = "A candidate, canonical attribute name, JSON typed-value object, and supporting excerpt are required.";
            return false;
        }

        if (page.HttpStatus is < 200 or >= 300 ||
            page.RetrievedAt == default ||
            page.RetrievedAt > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            error = "The page observation does not represent a valid successful retrieval.";
            return false;
        }

        if (page.FreshUntil is { } freshUntil && freshUntil <= DateTimeOffset.UtcNow)
        {
            error = $"The fetched page is stale; its freshness expired at {freshUntil:O}. Fetch it again before reviewing this claim.";
            return false;
        }

        if (!Uri.TryCreate(page.SourceUrl, UriKind.Absolute, out var sourceUri) ||
            sourceUri.Scheme is not ("http" or "https") ||
            string.IsNullOrWhiteSpace(sourceUri.Host) ||
            !string.IsNullOrEmpty(sourceUri.UserInfo) ||
            !sourceUri.IsDefaultPort)
        {
            error = "The page observation has an invalid or unsafe source URL.";
            return false;
        }

        if (!page.Text.Contains(excerpt, StringComparison.Ordinal))
        {
            error = "The supporting excerpt does not appear verbatim in the fetched page text.";
            return false;
        }

        evidence = new LatticeCandidateAttributeEvidence(
            candidate.Trim(),
            attributeName,
            value.Clone(),
            excerpt,
            sourceUri.AbsoluteUri,
            page.RetrievedAt)
        {
            FreshUntil = page.FreshUntil
        };
        return true;
    }
}
