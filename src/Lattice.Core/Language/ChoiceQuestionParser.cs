using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class ChoiceQuestionParser
{
    private static readonly Regex Pattern = new(
        @"^Which\s+is\s+a\s+species\s+of\s+(?<category>[A-Za-z][A-Za-z -]*?)\?\s*(?<first>[A-Za-z][A-Za-z-]*)\s+or\s+(?<second>[A-Za-z][A-Za-z-]*)\.?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static bool TryParse(
        string source,
        out SemanticUtterance? utterance)
    {
        var match = Pattern.Match(source.Trim());
        if (!match.Success)
        {
            utterance = null;
            return false;
        }

        var category = NormalizeIdentifier(match.Groups["category"].Value);
        var first = NormalizeIdentifier(match.Groups["first"].Value);
        var second = NormalizeIdentifier(match.Groups["second"].Value);

        if (category.Length == 0 ||
            first.Length == 0 ||
            second.Length == 0 ||
            string.Equals(first, second, StringComparison.OrdinalIgnoreCase))
        {
            utterance = null;
            return false;
        }

        utterance = new SemanticUtterance(
            UtteranceKind.Question,
            new ChoiceQuestionFormula(
                new SemanticIdentifier(category),
                ImmutableArray.Create(
                    new SemanticIdentifier(first),
                    new SemanticIdentifier(second))),
            source.Trim());
        return true;
    }

    private static string NormalizeIdentifier(string value) =>
        Regex.Replace(value.Trim(), @"\s+", "-").ToLowerInvariant();
}
