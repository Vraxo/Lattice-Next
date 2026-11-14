using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class ActivitySelectionParser
{
    private static readonly Regex Pattern = new(
        @"^Which of the following (?:is|are) useful for (?<activity>[A-Za-z][A-Za-z -]*?)\s*:\s*(?<items>.+?)[?.]?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static bool TryParse(string source, out SemanticUtterance? utterance)
    {
        var match = Pattern.Match(source.Trim());
        if (!match.Success)
        {
            utterance = null;
            return false;
        }

        var activity = Normalize(match.Groups["activity"].Value);
        var items = Regex.Split(
                match.Groups["items"].Value,
                @"\s*,\s*(?:and\s+)?|\s+and\s+",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .Select(Normalize)
            .Where(item => item.Length > 0)
            .Select(item => new SemanticIdentifier(item))
            .ToImmutableArray();

        if (activity.Length == 0 ||
            items.Length < 2 ||
            items.Select(item => item.Value).Distinct(StringComparer.OrdinalIgnoreCase).Count() != items.Length)
        {
            utterance = null;
            return false;
        }

        utterance = new SemanticUtterance(
            UtteranceKind.Question,
            new ActivitySelectionFormula(new SemanticIdentifier(activity), items),
            source.Trim());
        return true;
    }

    private static string Normalize(string value)
    {
        var normalized = value.Trim().TrimEnd('.', '?');
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
