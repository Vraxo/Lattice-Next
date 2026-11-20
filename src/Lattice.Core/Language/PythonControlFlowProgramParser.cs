using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class PythonControlFlowProgramParser
{
    private static readonly Regex LoopRangeOutputPattern = new(
        @"\b(?:print|display|output)s?\s+(?:each\s+)?(?<item>numbers?|values?|items?|counts?|i)\s+from\s+(?<start>\d+)\s+(?:to|through)\s+(?<end>\d+)\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex LoopCollectionOutputPattern = new(
        @"\b(?:print|display|output)s?\s+(?:each\s+)?(?<item>[A-Za-z_][A-Za-z0-9_]*)\s+(?:in|from)\s+(?:the\s+)?(?:list\s+)?(?<items>\[[^\]]*\])",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex GenericEvenListFilterPattern = new(
        @"\b(?:prints?|displays?|outputs?)\s+only\s+the\s+even\s+numbers?\s+from\s+(?:a|the)\s+list\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    internal static bool TryGetLoopProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        var collectionMatch = LoopCollectionOutputPattern.Match(source);
        if (collectionMatch.Success &&
            TryNormalizeListLiteral(collectionMatch.Groups["items"].Value, out var items))
        {
            var item = collectionMatch.Groups["item"].Value;
            var variable = item.EndsWith("s", StringComparison.OrdinalIgnoreCase)
                ? item[..^1]
                : item;

            steps = ImmutableArray.Create(
                new TaskRequestStep("begin-loop", "loop-header", $"{variable} in {items}"),
                new TaskRequestStep(
                    "write-standard-output-indented-expression",
                    "expression",
                    variable));
            return true;
        }

        var match = LoopRangeOutputPattern.Match(source);
        if (!match.Success ||
            !int.TryParse(match.Groups["start"].Value, out var start) ||
            !int.TryParse(match.Groups["end"].Value, out var inclusiveEnd) ||
            inclusiveEnd < start ||
            inclusiveEnd == int.MaxValue)
        {
            return false;
        }

        var rangeVariable = match.Groups["item"].Value.ToLowerInvariant() switch
        {
            "numbers" => "number",
            "values" => "value",
            "items" => "item",
            "counts" => "count",
            var item => item
        };
        var loopHeader = $"{rangeVariable} in range({start}, {inclusiveEnd + 1})";

        steps = ImmutableArray.Create(
            new TaskRequestStep("begin-loop", "loop-header", loopHeader),
            new TaskRequestStep(
                "write-standard-output-indented-expression",
                "expression",
                rangeVariable));
        return true;
    }

    internal static bool TryNormalizeListLiteral(string value, out string normalized)
    {
        normalized = value.Trim();
        const string number = @"[+-]?\d+(?:\.\d+)?";
        const string singleQuotedString = @"'(?:\\.|[^'\\])*'";
        const string doubleQuotedString = @"""(?:\\.|[^""\\])*""";
        var item = $@"(?:{number}|{singleQuotedString}|{doubleQuotedString})";
        if (!Regex.IsMatch(
            normalized,
            $@"^\[\s*{item}(?:\s*,\s*{item})*\s*\]$",
            RegexOptions.CultureInvariant))
        {
            return false;
        }

        normalized = Regex.Replace(normalized, @"\s*,\s*", ", ");
        return true;
    }

}
