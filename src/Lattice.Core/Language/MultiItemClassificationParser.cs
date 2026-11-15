using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class MultiItemClassificationParser
{
    private static readonly Regex Pattern = new(
        @"^Identify\\s+which\\s+(?<itemType>[A-Za-z][A-Za-z -]*?)\\s+is\\s+(?<firstCategory>[A-Za-z][A-Za-z -]*?)\\s+or\\s+(?<secondCategory>[A-Za-z][A-Za-z -]*?)\\s*:\\s*(?<items>[A-Za-z][A-Za-z -]*(?:,\\s*(?:and\\s+)?[A-Za-z][A-Za-z -]*)*)\\.?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex CategorizePattern = new(
        @"^Categorize\\s+each\\s+of\\s+the\\s+following\\s+(?<itemType>[A-Za-z][A-Za-z -]*?)\\s+as\\s+either\\s+(?<firstCategory>[A-Za-z][A-Za-z -]*?)\\s+or\\s+(?<secondCategory>[A-Za-z][A-Za-z -]*?)\\s*:\\s*(?<items>[A-Za-z][A-Za-z -]*(?:,\\s*(?:and\\s+)?[A-Za-z][A-Za-z -]*)*)\\.?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex UsefulForPurposePattern = new(
        @"^Which\\s+of\\s+the\\s+following\\s+is\\s+(?<category>[A-Za-z][A-Za-z-]*)\\s+for\\s+(?<purpose>[A-Za-z][A-Za-z -]*?)\\s*:\\s*(?<items>.+?)[?.]?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static bool TryParse(
        string source,
        out SemanticUtterance? utterance)
    {
        var trimmedSource = source.Trim();
        var purposeMatch = UsefulForPurposePattern.Match(trimmedSource);

        string itemType;
        ImmutableArray<SemanticIdentifier> categories;
        ImmutableArray<SemanticIdentifier> items;

        if (purposeMatch.Success)
        {
            var category = NormalizeIdentifier(purposeMatch.Groups["category"].Value);
            var purpose = NormalizeIdentifier(purposeMatch.Groups["purpose"].Value);
            itemType = $"thing-for-{purpose}";
            categories =
            [
                new SemanticIdentifier(category),
                new SemanticIdentifier($"not-{category}")
            ];
            items = ParseItems(purposeMatch.Groups["items"].Value, removeArticles: true);
        }
        else
        {
            var match = Pattern.Match(trimmedSource);
            if (!match.Success)
            {
                match = CategorizePattern.Match(trimmedSource);
            }

            if (!match.Success)
            {
                utterance = null;
                return false;
            }

            itemType = NormalizePluralIdentifier(NormalizeIdentifier(match.Groups["itemType"].Value));
            categories =
            [
                new SemanticIdentifier(NormalizeIdentifier(match.Groups["firstCategory"].Value)),
                new SemanticIdentifier(NormalizeIdentifier(match.Groups["secondCategory"].Value))
            ];
            items = ParseItems(match.Groups["items"].Value, removeArticles: false);
        }

        if (itemType.Length == 0 ||
            categories.Any(category => category.Value.Length == 0) ||
            categories.Select(category => category.Value).Distinct(StringComparer.OrdinalIgnoreCase).Count() != categories.Length ||
            items.Length < 2 ||
            items.Any(item => item.Value.Length == 0) ||
            items.Select(item => item.Value).Distinct(StringComparer.OrdinalIgnoreCase).Count() != items.Length)
        {
            utterance = null;
            return false;
        }

        utterance = new SemanticUtterance(
            UtteranceKind.Question,
            new MultiItemClassificationFormula(
                new SemanticIdentifier(itemType),
                categories,
                items),
            trimmedSource);
        return true;
    }

    private static ImmutableArray<SemanticIdentifier> ParseItems(
        string value,
        bool removeArticles) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => item.StartsWith("and ", StringComparison.OrdinalIgnoreCase)
                ? item[4..]
                : item)
            .Select(item => removeArticles
                ? Regex.Replace(item, @"^(?:a|an|the)\\s+", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                : item)
            .Select(NormalizeIdentifier)
            .Select(item => new SemanticIdentifier(item))
            .ToImmutableArray();

    private static string NormalizeIdentifier(string value) =>
        Regex.Replace(value.Trim(), @"\\s+", "-").ToLowerInvariant();

    private static string NormalizePluralIdentifier(string value) =>
        value is "species" or "series"
            ? value
            : value.EndsWith("ies", StringComparison.Ordinal)
                ? value[..^3] + "y"
                : value.EndsWith("s", StringComparison.Ordinal) &&
                    !value.EndsWith("ss", StringComparison.Ordinal)
                    ? value[..^1]
                    : value;
}
