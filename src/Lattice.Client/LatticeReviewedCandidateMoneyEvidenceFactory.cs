namespace Lattice.Client;

/// <summary>Creates candidate evidence only from a user-selected excerpt in a fetched page.</summary>
public static class LatticeReviewedCandidateMoneyEvidenceFactory
{
    public static bool TryCreate(
        LatticeExternalPageObservation page,
        string candidate,
        decimal amount,
        string currencySymbol,
        string excerpt,
        out LatticeCandidateMoneyEvidence? evidence,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(page);
        evidence = null;
        error = null;

        if (string.IsNullOrWhiteSpace(candidate) || candidate.Length > 200 ||
            amount < 0m ||
            string.IsNullOrWhiteSpace(currencySymbol) || currencySymbol.Length > 8 ||
            string.IsNullOrWhiteSpace(excerpt) || excerpt.Length > 4000)
        {
            error = "Candidate, amount, currency, or supporting excerpt is invalid.";
            return false;
        }

        if (page.FreshUntil is { } freshUntil && freshUntil <= DateTimeOffset.UtcNow)
        {
            error = $"The fetched page is stale; its freshness expired at {freshUntil:O}. Fetch it again before reviewing this claim.";
            return false;
        }

        if (!page.Text.Contains(excerpt, StringComparison.Ordinal))
        {
            error = "The supporting excerpt must appear exactly in the fetched page text.";
            return false;
        }

        evidence = new LatticeCandidateMoneyEvidence(
            candidate.Trim(),
            amount,
            currencySymbol.Trim(),
            excerpt,
            page.SourceUrl,
            page.RetrievedAt)
        {
            FreshUntil = page.FreshUntil
        };
        return true;
    }
}
