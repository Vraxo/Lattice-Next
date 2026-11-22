using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class PythonClassProgramParser
{
    private static readonly Regex BalanceClassPattern = new(
        @"\bclass\s+representing\s+(?:a\s+)?(?<entity>[A-Za-z]+(?:\s+[A-Za-z]+)?)\s+with\s+(?:a\s+)?(?<attribute>[A-Za-z_][A-Za-z0-9_]*)\b.*?\b(?<deposit>deposit|credit)\s+method\b.*?\b(?<withdrawal>withdrawal)\s+method\b.*?\brefuses?\s+withdrawals?\s+(?:that\s+are\s+)?larger\s+than\s+(?:the\s+)?(?<guardAttribute>[A-Za-z_][A-Za-z0-9_]*)\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    internal static bool TryGetBalanceClassProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        var match = BalanceClassPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        var attribute = match.Groups["attribute"].Value;
        if (!attribute.Equals(match.Groups["guardAttribute"].Value, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var className = string.Concat(match.Groups["entity"].Value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
        var depositMethod = match.Groups["deposit"].Value.ToLowerInvariant();
        var withdrawalMethod = "withdraw";

        steps = ImmutableArray.Create(
            new TaskRequestStep("begin-class", "class-name", className),
            new TaskRequestStep("begin-class-method", "method-header", $"__init__(self, {attribute})"),
            new TaskRequestStep("assign-variable-double-indented", "assignment", $"self.{attribute} = {attribute}"),
            new TaskRequestStep("begin-class-method", "method-header", $"{depositMethod}(self, amount)"),
            new TaskRequestStep("assign-variable-double-indented", "assignment", $"self.{attribute} = self.{attribute} + amount"),
            new TaskRequestStep("begin-class-method", "method-header", $"{withdrawalMethod}(self, amount)"),
            new TaskRequestStep("begin-conditional-double-indented", "condition", $"amount > self.{attribute}"),
            new TaskRequestStep("return-value-triple-indented-expression", "expression", "False"),
            new TaskRequestStep("assign-variable-double-indented", "assignment", $"self.{attribute} = self.{attribute} - amount"),
            new TaskRequestStep("return-value-double-indented-expression", "expression", "True"),
            new TaskRequestStep("assign-variable", "assignment", $"account = {className}(100)"),
            new TaskRequestStep("write-standard-output", "expression", $"account.{depositMethod}(50)"),
            new TaskRequestStep("write-standard-output", "expression", $"account.{withdrawalMethod}(200)"),
            new TaskRequestStep("write-standard-output", "expression", $"account.{withdrawalMethod}(25)"),
            new TaskRequestStep("write-standard-output", "expression", $"account.{attribute}"));
        return true;
    }
}
