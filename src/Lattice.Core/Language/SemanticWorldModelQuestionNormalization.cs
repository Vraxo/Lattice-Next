using System.Text.RegularExpressions;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class SemanticWorldModelQuestionNormalization
{
    internal static string SingularizeEnumerationGroup(string group) =>
        group switch
        {
            "children" => "child",
            "people" => "person",
            "men" => "man",
            "women" => "woman",
            "mice" => "mouse",
            "geese" => "goose",
            "teeth" => "tooth",
            "feet" => "foot",
            _ when group.EndsWith("ies", StringComparison.Ordinal) => $"{group[..^3]}y",
            _ when group.EndsWith("sses", StringComparison.Ordinal) ||
                   group.EndsWith("shes", StringComparison.Ordinal) ||
                   group.EndsWith("ches", StringComparison.Ordinal) ||
                   group.EndsWith("xes", StringComparison.Ordinal) ||
                   group.EndsWith("zes", StringComparison.Ordinal) => group[..^2],
            _ when group.EndsWith("s", StringComparison.Ordinal) &&
                   !group.EndsWith("ss", StringComparison.Ordinal) => group[..^1],
            _ => group
        };

    internal static bool TryParseCardinal(string value, out int number)
    {
        if (int.TryParse(value, out number))
        {
            return number > 0;
        }

        number = value.ToLowerInvariant() switch
        {
            "one" => 1,
            "two" => 2,
            "three" => 3,
            "four" => 4,
            "five" => 5,
            "six" => 6,
            "seven" => 7,
            "eight" => 8,
            "nine" => 9,
            "ten" => 10,
            _ => 0
        };

        return number > 0;
    }

    internal static bool TryParseOrdinal(string value, out int number)
    {
        number = value.ToLowerInvariant() switch
        {
            "first" => 1,
            "second" => 2,
            "third" => 3,
            "fourth" => 4,
            "fifth" => 5,
            "sixth" => 6,
            "seventh" => 7,
            "eighth" => 8,
            "ninth" => 9,
            "tenth" => 10,
            _ => 0
        };

        if (number > 0)
        {
            return true;
        }

        var digits = Regex.Match(value, @"^(?<number>[0-9]+)(?:st|nd|rd|th)$");
        return digits.Success && int.TryParse(digits.Groups["number"].Value, out number) && number > 0;
    }

    internal static string NormalizeMaximumMetric(string value) =>
        Normalize(value) switch
        {
            "olympic-gold-medal" or "olympic-gold-medals" => "olympic-gold-medal-count",
            _ => Normalize(value)
        };

    internal static string NormalizeListCategory(string value) =>
        Normalize(value) switch
        {
            "golf-equipment-companies" or "golf-equipment-company-names" => "golf-equipment-company",
            "golf-equipment-company" => "golf-equipment-company",
            _ => Normalize(value)
        };

    internal static string NormalizeBeginnerActivity(string value) =>
        Normalize(value) switch
        {
            "run" or "running" or "jogging" => "running",
            _ => Normalize(value)
        };

    internal static string NormalizeHarmTerm(string value) =>
        Normalize(value) switch
        {
            "mobile" or "mobile-phone" or "mobile-phones" or
            "smartphone" or "smartphones" or "smartphone-use" or "phone-use" or
            "cell-phone" or "cell-phones" => "mobile-phone-use",
            "human" or "humans" or "people" => "person",
            _ => Normalize(value)
        };

    internal static string NormalizeContributionTerm(string value) =>
        Normalize(value) switch
        {
            "un" => "united-nations",
            "ny" => "new-york-city",
            "land" => "land-parcel",
            "hq" or "headquarters" => "headquarters-site",
            _ => Normalize(value)
        };

    internal static string NormalizeQuotedTerm(string value) =>
        Normalize(value.Trim().Trim('"', '\u201c', '\u201d', '\u0027', '\u2019'));

    internal static string NormalizeRegionalVariant(string value) =>
        Normalize(value) switch
        {
            "american" or "americans" or "united-states" => "american-english",
            "british" or "brits" or "united-kingdom" => "british-english",
            _ => Normalize(value)
        };

    internal static string NormalizeType(string value)
    {
        var normalized = Normalize(value);
        if (normalized.EndsWith("ies", StringComparison.Ordinal) && normalized.Length > 3)
        {
            return normalized[..^3] + "y";
        }

        return normalized.EndsWith('s') && !normalized.EndsWith("ss", StringComparison.Ordinal)
            ? normalized[..^1]
            : normalized;
    }

    internal static string Normalize(string value)
    {
        var normalized = value.Trim();
        foreach (var article in new[] { "a ", "an ", "the " })
        {
            if (normalized.StartsWith(article, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[article.Length..];
                break;
            }
        }

        return SemanticWorldModel.Normalize(normalized);
    }
}
