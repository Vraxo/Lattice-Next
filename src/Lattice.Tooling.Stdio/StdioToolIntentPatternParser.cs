using System.Text.RegularExpressions;
using Lattice.Core.Tools;

namespace Lattice.Tooling.Stdio;

internal static class StdioToolIntentPatternParser
{
        internal static bool TryGetPatternSlots(
            string pattern,
            out Dictionary<string, string> slots)
        {
            slots = new Dictionary<string, string>(StringComparer.Ordinal);
    
            if (!ToolIntentPatternParser.TryParse(pattern, out var parts))
            {
                return false;
            }
    
            foreach (var part in parts.Where(part => part.SlotName is not null))
            {
                slots.Add(part.SlotName!, part.SlotType!);
            }
    
            return slots.Count > 0;
        }

        internal static bool MatchesIntentSlotType(ToolInputType inputType, string slotType) =>
            slotType switch
            {
                "number" => inputType == ToolInputType.Number,
                "word" or "text" => inputType == ToolInputType.String,
                _ => false
            };

        internal static bool TryValidateResponseTemplate(
            string response,
            IEnumerable<string> argumentNames)
        {
            var available = argumentNames
                .Append("result")
                .ToHashSet(StringComparer.Ordinal);
            var matches = Regex.Matches(
                response,
                "\\{(?<name>[A-Za-z][A-Za-z0-9_]*)\\}",
                RegexOptions.CultureInvariant);
    
            var remaining = Regex.Replace(
                response,
                "\\{(?<name>[A-Za-z][A-Za-z0-9_]*)\\}",
                string.Empty,
                RegexOptions.CultureInvariant);
    
            if (remaining.Contains('{') || remaining.Contains('}'))
            {
                return false;
            }
    
            return matches.Count > 0 &&
                matches.Cast<Match>().All(match => available.Contains(match.Groups["name"].Value)) &&
                matches.Cast<Match>().Any(match => match.Groups["name"].Value == "result");
        }
}
