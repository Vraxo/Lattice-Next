using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class PythonCollectionProgramParser
{

    private static readonly Regex CollectionFilterFunctionPattern = new(
        @"\bfunction\s+(?:named\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s+)?(?:that\s+)?(?:accepts?|receives?)\s+(?:a\s+)?list\s+of\s+numbers?(?:\s+(?:named\s+)?(?<parameter>[A-Za-z_][A-Za-z0-9_]*))?\s+and\s+returns?\s+(?:a\s+)?new\s+list\s+containing\s+only\s+(?<item>[A-Za-z_][A-Za-z0-9_]*)(?:\s+values?)?\s+(?<comparison>greater\s+than\s+or\s+equal\s+to|at\s+least|less\s+than\s+or\s+equal\s+to|at\s+most|not\s+equal\s+to|greater\s+than|more\s+than|less\s+than|below|equal\s+to|equals)\s+(?<threshold>(?:the\s+)?average(?:\s+of\s+(?:the\s+)?(?<averageParameter>[A-Za-z_][A-Za-z0-9_]*))?|\d+(?:\.\d+)?(?:\s*[+*/%\-]\s*\d+(?:\.\d+)?)*)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    internal static bool TryGetCollectionFilterFunctionProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        var match = CollectionFilterFunctionPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        var threshold = match.Groups["threshold"].Value;
        var isAverage = threshold.Contains("average", StringComparison.OrdinalIgnoreCase);
        var name = match.Groups["name"].Success
            ? match.Groups["name"].Value
            : isAverage ? "above_average" : "filter_values";
        var parameter = match.Groups["parameter"].Success ? match.Groups["parameter"].Value : "values";
        var item = match.Groups["item"].Value;
        if (item.Equals(parameter, StringComparison.OrdinalIgnoreCase) &&
            item.EndsWith("s", StringComparison.OrdinalIgnoreCase))
        {
            item = item[..^1];
        }

        var averageParameter = match.Groups["averageParameter"].Success
            ? match.Groups["averageParameter"].Value
            : parameter;

        if (item.Equals(parameter, StringComparison.OrdinalIgnoreCase) ||
            (isAverage && !averageParameter.Equals(parameter, StringComparison.OrdinalIgnoreCase)) ||
            name.Equals(parameter, StringComparison.OrdinalIgnoreCase) ||
            name.Equals(item, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var comparisonText = Regex.Replace(
            match.Groups["comparison"].Value,
            @"\s+",
            " ",
            RegexOptions.CultureInvariant).Trim().ToLowerInvariant();
        var comparison = comparisonText switch
        {
            "greater than" or "more than" => ">",
            "greater than or equal to" or "at least" => ">=",
            "less than" or "below" => "<",
            "less than or equal to" or "at most" => "<=",
            "equal to" or "equals" => "==",
            "not equal to" => "!=",
            _ => string.Empty
        };
        var thresholdExpression = isAverage
            ? $"sum({parameter}) / len({parameter})"
            : threshold;
        if (comparison.Length == 0 || (!isAverage && !PythonExpressionParser.IsArithmeticExpression(thresholdExpression)))
        {
            return false;
        }
        var expression = $"[{item} for {item} in {parameter} if {item} {comparison} {thresholdExpression}]";
        steps = ImmutableArray.Create(
            new TaskRequestStep("begin-function", "function-header", $"{name}({parameter})"),
            new TaskRequestStep("return-value-indented-expression", "expression", expression),
            new TaskRequestStep("write-standard-output", "expression", $"{name}([1, 2, 3, 4, 5])"));
        return true;
    }

    private static bool TryNormalizeSimpleDictionaryLiteral(string value, out string normalized)
    {
        normalized = value.Trim();
        const string key = @"(?:'[^'\\]*'|""[^""\\]*"")";
        const string number = @"[+-]?\d+(?:\.\d+)?";
        if (!Regex.IsMatch(
            normalized,
            $@"^\{{\s*{key}\s*:\s*{number}(?:\s*,\s*{key}\s*:\s*{number})*\s*\}}$",
            RegexOptions.CultureInvariant))
        {
            return false;
        }

        normalized = Regex.Replace(normalized, @"\s*:\s*", ": ");
        normalized = Regex.Replace(normalized, @"\s*,\s*", ", ");
        return true;
    }

}
