using System.Globalization;
using System.Text.Json;

namespace Lattice.Client;

/// <summary>Parses the metadata envelope emitted by the bounded web-page-fetch tool.</summary>
public static class LatticeExternalPageObservationParser
{
    public static bool TryParse(
        string content,
        out LatticeExternalPageObservation? observation,
        out string? error)
    {
        observation = null;
        error = null;

        if (string.IsNullOrWhiteSpace(content))
        {
            error = "The page-fetch observation is empty.";
            return false;
        }

        var page = content;
        try
        {
            using var document = JsonDocument.Parse(content);
            if (document.RootElement.ValueKind == JsonValueKind.String)
            {
                page = document.RootElement.GetString() ?? string.Empty;
            }
        }
        catch (JsonException)
        {
        }

        var lines = page.Split('\n');
        if (!TryGetValue(lines, "Final URL: ", out var sourceUrlText) ||
            !TryGetValue(lines, "Retrieved at: ", out var retrievedAtText) ||
            !TryGetValue(lines, "HTTP status: ", out var statusText) ||
            !TryGetValue(lines, "Content-Type: ", out var contentType))
        {
            error = "The page-fetch observation is missing required source metadata.";
            return false;
        }

        if (!Uri.TryCreate(sourceUrlText, UriKind.Absolute, out var sourceUrl) ||
            sourceUrl.Scheme is not ("http" or "https") ||
            string.IsNullOrWhiteSpace(sourceUrl.Host) ||
            !string.IsNullOrEmpty(sourceUrl.UserInfo) ||
            !sourceUrl.IsDefaultPort)
        {
            error = "The page-fetch observation contains an invalid or unsafe source URL.";
            return false;
        }

        if (!DateTimeOffset.TryParse(
                retrievedAtText,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var retrievedAt) ||
            retrievedAt == default ||
            retrievedAt > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            error = "The page-fetch observation contains an invalid retrieval timestamp.";
            return false;
        }

        if (!int.TryParse(statusText, NumberStyles.None, CultureInfo.InvariantCulture, out var status))
        {
            error = "The page-fetch observation contains an invalid HTTP status code.";
            return false;
        }

        if (status is < 200 or >= 300)
        {
            error = GetHttpStatusError(status);
            return false;
        }

        var mediaType = contentType.Split(';', 2)[0].Trim();
        if (!mediaType.Equals("text/html", StringComparison.OrdinalIgnoreCase) &&
            !mediaType.Equals("application/xhtml+xml", StringComparison.OrdinalIgnoreCase) &&
            !mediaType.Equals("text/plain", StringComparison.OrdinalIgnoreCase))
        {
            error = "The page-fetch observation has an unsupported content type.";
            return false;
        }

        const string textMarker = "Extracted page text:";
        var markerIndex = page.IndexOf(textMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            error = "The page-fetch observation does not contain extracted page text.";
            return false;
        }

        var text = page[(markerIndex + textMarker.Length)..].TrimStart('\r', '\n').Trim();
        if (string.IsNullOrWhiteSpace(text) || text.Length > 12_100)
        {
            error = "The page-fetch observation contains empty or oversized extracted text.";
            return false;
        }

        DateTimeOffset? freshUntil = null;
        if (TryGetValue(lines, "Fresh until: ", out var freshUntilText))
        {
            if (!DateTimeOffset.TryParse(
                    freshUntilText,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var parsedFreshUntil))
            {
                error = "The page-fetch observation contains an invalid freshness expiry.";
                return false;
            }

            freshUntil = parsedFreshUntil;
        }

        var titleLine = lines.FirstOrDefault(line => line.StartsWith("Page title: ", StringComparison.Ordinal));
        var title = titleLine is null ? null : titleLine["Page title: ".Length..];
        observation = new LatticeExternalPageObservation(
            sourceUrlText,
            retrievedAt,
            status,
            contentType,
            text,
            title,
            freshUntil);
        return true;
    }

    private static string GetHttpStatusError(int status) =>
        status switch
        {
            401 or 403 => $"The requested page is inaccessible (HTTP {status}).",
            404 or 410 => $"The requested page is unavailable (HTTP {status}).",
            408 or 425 or 429 or >= 500 =>
                $"The page source is temporarily unavailable (HTTP {status}).",
            _ => $"The page fetch did not return a successful HTTP response (HTTP {status})."
        };

    private static bool TryGetValue(string[] lines, string prefix, out string value)
    {
        var line = lines.FirstOrDefault(item => item.StartsWith(prefix, StringComparison.Ordinal));
        value = line is null ? string.Empty : line[prefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(value);
    }
}
