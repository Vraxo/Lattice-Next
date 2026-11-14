using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class FamilySetCompletionParser
{
    private static readonly Regex Pattern = new(
        @"^(?<anchor>[A-Z][A-Za-z'-]*)['’]s parents have (?<count>one|two|three|four|five|six|seven|eight|nine|ten|[0-9]+) (?<memberType>daughters|sons|children):\s*(?<members>.+?),\s+and what(?:['’]s| is) the name of the (?<ordinal>first|second|third|fourth|fifth|sixth|seventh|eighth|ninth|tenth|[0-9]+(?:st|nd|rd|th)) (?<requestedType>daughter|son|child)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex NamePattern = new(
        @"^[A-Z][A-Za-z'-]*(?:\s+[A-Z][A-Za-z'-]*)*$",
        RegexOptions.CultureInvariant);

    internal static bool TryParse(string source, out SemanticUtterance? utterance)
    {
        utterance = null;
        var match = Pattern.Match(source.Trim());
        if (!match.Success)
        {
            return false;
        }

        if (!TryParseNumber(match.Groups["count"].Value, out var expectedCount) ||
            !TryParseOrdinal(match.Groups["ordinal"].Value, out var requestedOrdinal))
        {
            return false;
        }

        var memberType = NormalizeMemberType(match.Groups["memberType"].Value);
        var requestedType = NormalizeMemberType(match.Groups["requestedType"].Value);
        if (memberType != requestedType)
        {
            return false;
        }

        var knownMembers = match.Groups["members"].Value
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (knownMembers.Length == 0 || knownMembers.Any(name => !NamePattern.IsMatch(name)))
        {
            return false;
        }

        utterance = new SemanticUtterance(
            UtteranceKind.Question,
            new FamilySetCompletionQuestionFormula(
                SemanticWorldModel.Normalize(match.Groups["anchor"].Value),
                "parents",
                memberType,
                expectedCount,
                requestedOrdinal,
                knownMembers
                    .Select(SemanticWorldModel.Normalize)
                    .ToImmutableArray()),
            source.Trim());
        return true;
    }

    private static string NormalizeMemberType(string value) =>
        value.ToLowerInvariant() switch
        {
            "daughters" or "daughter" => "daughter",
            "sons" or "son" => "son",
            "children" or "child" => "child",
            _ => string.Empty
        };

    private static bool TryParseNumber(string value, out int number)
    {
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

        return number > 0 || int.TryParse(value, out number) && number > 0;
    }

    private static bool TryParseOrdinal(string value, out int ordinal)
    {
        ordinal = value.ToLowerInvariant() switch
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

        if (ordinal > 0)
        {
            return true;
        }

        var digits = Regex.Match(value, @"^(?<number>[0-9]+)(?:st|nd|rd|th)$");
        return digits.Success && int.TryParse(digits.Groups["number"].Value, out ordinal) && ordinal > 0;
    }
}
