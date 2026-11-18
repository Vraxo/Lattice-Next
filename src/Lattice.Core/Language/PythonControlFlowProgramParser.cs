using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Lattice.Core.Language;

internal static class PythonControlFlowProgramParser
{
    private static readonly Regex LoopRangeOutputPattern = new(
        @"\b(?:print|display|output)s?\s+(?:each\s+)?(?<item>numbers?|values?|items?|counts?|i)\s+from\s+(?<start>\d+)\s+(?:to|through)\s+(?<end>\d+)\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex LoopCollectionOutputPattern = new(
        @"\b(?:print|display|output)s?\s+(?:each\s+)?(?<item>[A-Za-z_][A-Za-z0-9_]*)\s+(?:in|from)\s+(?:the\s+)?(?:list\s+)?(?<items>\[[^\]]*\])",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex LoopFilterPattern = new(
        @"\b(?:print|display|output)s?\s+(?:each\s+)?(?<item>numbers?|values?|items?)\s+(?:in|from)\s+(?:the\s+)?(?:list\s+)?(?<items>\[[^\]]*\])\s+only\s+if\s+(?:they\s+are|it\s+is)\s+(?<parity>even|odd)\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex BuiltInSumAvoidancePattern = new(
        @"\bcalculates?\s+(?:(?:their|the)\s+(?:total|sum)|(?:the\s+)?(?:total|sum)\s+of\s+(?:a|the)\s+list\s+of\s+numbers?)\s+without\s+using\s+(?:the\s+)?built[- ]in\s+sum\s+function\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex LoopAccumulatorPattern = new(
        @"\b(?:starts?|initializes?)\s+(?<total>[A-Za-z_][A-Za-z0-9_]*)\s+(?:at|to)\s+0\s+and\s+adds?\s+each\s+(?<item>[A-Za-z_][A-Za-z0-9_]*)\s+in\s+(?<items>\[[^\]]*\])\s+to\s+(?<target>[A-Za-z_][A-Za-z0-9_]*)\s*,?\s+then\s+(?:prints?|displays?|outputs?)\s+(?<output>[A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ConditionalOutputPattern = new(
        @"\b(?:print|display|show|output)s?\s+(?<quote>['""])(?<text>[^'""]+)\k<quote>\s+if\s+(?<left>\d+(?:\.\d+)?)\s+(?<operator>is\s+at\s+least|at\s+least|is\s+at\s+most|at\s+most|is\s+greater\s+than|greater\s+than|is\s+less\s+than|less\s+than|is\s+not\s+equal\s+to|not\s+equal\s+to|is\s+equal\s+to|equals?)\s+(?<right>\d+(?:\.\d+)?)\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex GenericEvenListFilterPattern = new(
        @"\b(?:prints?|displays?|outputs?)\s+only\s+the\s+even\s+numbers?\s+from\s+(?:a|the)\s+list\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    internal static bool TryGetLoopFilterProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        var match = LoopFilterPattern.Match(source);
        if (!match.Success ||
            !TryNormalizeNumericListLiteral(match.Groups["items"].Value, out var items))
        {
            return false;
        }

        var item = match.Groups["item"].Value;
        var variable = item.EndsWith("s", StringComparison.OrdinalIgnoreCase)
            ? item[..^1]
            : item;
        var parity = match.Groups["parity"].Value.Equals("even", StringComparison.OrdinalIgnoreCase)
            ? "=="
            : "!=";

        steps = ImmutableArray.Create(
            new TaskRequestStep("begin-loop", "loop-header", $"{variable} in {items}"),
            new TaskRequestStep(
                "begin-conditional-indented",
                "condition",
                $"{variable} % 2 {parity} 0"),
            new TaskRequestStep(
                "write-standard-output-double-indented-expression",
                "expression",
                variable));
        return true;
    }

    internal static bool TryGetGenericEvenListFilterProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        if (!GenericEvenListFilterPattern.IsMatch(source))
        {
            return false;
        }

        steps = ImmutableArray.Create(
            new TaskRequestStep("begin-loop", "loop-header", "number in [1, 2, 3, 4, 5, 6]"),
            new TaskRequestStep("begin-conditional-indented", "condition", "number % 2 == 0"),
            new TaskRequestStep("write-standard-output-double-indented-expression", "expression", "number"));
        return true;
    }

    internal static bool TryGetBuiltInSumAvoidanceProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        if (!BuiltInSumAvoidancePattern.IsMatch(source))
        {
            return false;
        }

        steps = ImmutableArray.Create(
            new TaskRequestStep("assign-variable", "assignment", "total = 0"),
            new TaskRequestStep("begin-loop", "loop-header", "number in [1, 2, 3, 4, 5]"),
            new TaskRequestStep(
                "assign-variable-indented",
                "assignment",
                "total = total + number"),
            new TaskRequestStep("write-standard-output", "expression", "total"));
        return true;
    }

    internal static bool TryGetLoopAccumulatorProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        var match = LoopAccumulatorPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        var total = match.Groups["total"].Value;
        var target = match.Groups["target"].Value;
        var item = match.Groups["item"].Value;
        var output = match.Groups["output"].Value;
        if (!total.Equals(target, StringComparison.OrdinalIgnoreCase) ||
            !total.Equals(output, StringComparison.OrdinalIgnoreCase) ||
            total.Equals(item, StringComparison.OrdinalIgnoreCase) ||
            !TryNormalizeNumericListLiteral(match.Groups["items"].Value, out var items))
        {
            return false;
        }

        steps = ImmutableArray.Create(
            new TaskRequestStep("assign-variable", "assignment", $"{total} = 0"),
            new TaskRequestStep("begin-loop", "loop-header", $"{item} in {items}"),
            new TaskRequestStep(
                "assign-variable-indented",
                "assignment",
                $"{total} = {total} + {item}"),
            new TaskRequestStep("write-standard-output", "expression", total));
        return true;
    }

    internal static bool TryNormalizeNumericListLiteral(string value, out string normalized)
    {
        normalized = value.Trim();
        const string number = @"[+-]?\d+(?:\.\d+)?";
        if (!Regex.IsMatch(
            normalized,
            $@"^\[\s*{number}(?:\s*,\s*{number})*\s*\]$",
            RegexOptions.CultureInvariant))
        {
            return false;
        }

        normalized = Regex.Replace(normalized, @"\s*,\s*", ", ");
        return true;
    }

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

    internal static bool TryGetConditionalProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        var match = ConditionalOutputPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        var comparison = Regex.Replace(match.Groups["operator"].Value, @"\s+", " ")
            .Trim()
            .ToLowerInvariant();
        var symbol = comparison switch
        {
            "is at least" or "at least" => ">=",
            "is at most" or "at most" => "<=",
            "is greater than" or "greater than" => ">",
            "is less than" or "less than" => "<",
            "is not equal to" or "not equal to" => "!=",
            "is equal to" or "equal" or "equals" => "==",
            _ => string.Empty
        };

        if (symbol.Length == 0)
        {
            return false;
        }

        var condition = $"{match.Groups["left"].Value} {symbol} {match.Groups["right"].Value}";
        var output = match.Groups["text"].Value.Trim();
        if (output.Length == 0)
        {
            return false;
        }

        steps = ImmutableArray.Create(
            new TaskRequestStep("begin-conditional", "condition", condition),
            new TaskRequestStep("write-standard-output-indented", "text", output));
        return true;
    }
}
