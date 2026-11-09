using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Lattice.Core.Tools;

public static class ToolIntentPatternParser
{
    private static readonly Regex PartPattern = new(
        @"\[(?<optional>[^\[\]{}]+)\]|\{(?<slot>[A-Za-z][A-Za-z0-9_]*:(?:number|word|text))\}|(?<literal>[^\s\[\]{}]+)",
        RegexOptions.CultureInvariant);

    private static readonly Regex SlotPattern = new(
        @"^(?<name>[A-Za-z][A-Za-z0-9_]*):(?<type>number|word|text)$",
        RegexOptions.CultureInvariant);

    public static bool TryParse(
        string pattern,
        out ImmutableArray<ToolIntentPatternPart> parts)
    {
        parts = ImmutableArray<ToolIntentPatternPart>.Empty;

        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        var parsed = ImmutableArray.CreateBuilder<ToolIntentPatternPart>();
        var slots = new HashSet<string>(StringComparer.Ordinal);
        var position = 0;

        foreach (Match match in PartPattern.Matches(pattern))
        {
            if (!string.IsNullOrWhiteSpace(pattern[position..match.Index]))
            {
                return false;
            }

            if (match.Groups["optional"].Success)
            {
                var optionalTokens = SplitTokens(match.Groups["optional"].Value[1..^1]);

                if (optionalTokens.Length == 0)
                {
                    return false;
                }

                parsed.Add(new ToolIntentPatternPart(
                    optionalTokens,
                    IsOptional: true));
            }
            else if (match.Groups["slot"].Success)
            {
                var slotMatch = SlotPattern.Match(match.Groups["slot"].Value);

                if (!slotMatch.Success ||
                    !slots.Add(slotMatch.Groups["name"].Value))
                {
                    return false;
                }

                parsed.Add(new ToolIntentPatternPart(
                    ImmutableArray<string>.Empty,
                    slotMatch.Groups["name"].Value,
                    slotMatch.Groups["type"].Value));
            }
            else
            {
                parsed.Add(new ToolIntentPatternPart(
                    [match.Groups["literal"].Value]));
            }

            position = match.Index + match.Length;
        }

        if (!string.IsNullOrWhiteSpace(pattern[position..]) || slots.Count == 0)
        {
            return false;
        }

        parts = parsed.ToImmutable();
        return true;
    }

    private static ImmutableArray<string> SplitTokens(string value) =>
        value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .ToImmutableArray();
}
