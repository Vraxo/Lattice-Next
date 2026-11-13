using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class MultiItemClassificationParser
{
    private static readonly Regex Pattern = new(
        @"^Identify\s+which\s+(?<itemType>[A-Za-z][A-Za-z -]*?)\s+is\s+(?<firstCategory>[A-Za-z][A-Za-z -]*?)\s+or\s+(?<secondCategory>[A-Za-z][A-Za-z -]*?)\s*:\s*(?<items>[A-Za-z][A-Za-z -]*(?:,\s*(?:and\s+)?[A-Za-z][A-Za-z -]*)*)\.?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex CategorizePattern = new(
        @"^Categorize\s+each\s+of\s+the\s+following\s+(?<itemType>[A-Za-z][A-Za-z -]*?)\s+as\s+either\s+(?<firstCategory>[A-Za-z][A-Za-z -]*?)\s+or\s+(?<secondCategory>[A-Za-z][A-Za-z -]*?)\s*:\s*(?<items>[A-Za-z][A-Za-z -]*(?:,\s*(?:and\s+)?[A-Za-z][A-Za-z -]*)*)\.?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static bool TryParse(
        string source,
        out SemanticUtterance? utterance)
    {
        var trimmedSource = source.Trim();
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

        var itemType = NormalizePluralIdentifier(NormalizeIdentifier(match.Groups["itemType"].Value));
        var categories = ImmutableArray.Create(
            new SemanticIdentifier(NormalizeIdentifier(match.Groups["firstCategory"].Value)),
            new SemanticIdentifier(NormalizeIdentifier(match.Groups["secondCategory"].Value)));
        var items = match.Groups["items"].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => NormalizeIdentifier(value.StartsWith("and ", StringComparison.OrdinalIgnoreCase)
                ? value[4..]
                : value))
            .Select(value => new SemanticIdentifier(value))
            .ToImmutableArray();

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
            source.Trim());
        return true;
    }

    private static string NormalizeIdentifier(string value) =>
        Regex.Replace(value.Trim(), @"\s+", "-").ToLowerInvariant();

    private static string NormalizePluralIdentifier(string value) =>
        value.EndsWith("ies", StringComparison.Ordinal)
            ? value[..^3] + "y"
            : value.EndsWith("s", StringComparison.Ordinal) &&
                !value.EndsWith("ss", StringComparison.Ordinal)
                ? value[..^1]
                : value;
}
