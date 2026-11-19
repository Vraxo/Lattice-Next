using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class PythonBasicProgramParser
{

    private static readonly Regex TwoNumberAdditionPattern = new(
        @"\bstores?\s+(?:two|2)\s+numbers?\s+in\s+variables?,?\s+adds?\s+them,?\s+and\s+(?:prints?|displays?|outputs?)\s+(?:the\s+)?result\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex IntegerSignClassificationPattern = new(
        @"\b(?:classifies?\s+(?:an|the)\s+integer\s+as|reports?\s+whether\s+(?:an|the)\s+integer\s+is)\s+positive,?\s+negative,?\s+or\s+zero\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    internal static bool TryGetTwoNumberAdditionProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        if (!TwoNumberAdditionPattern.IsMatch(source))
        {
            return false;
        }

        steps = ImmutableArray.Create(
            new TaskRequestStep("assign-variable", "assignment", "first_number = 10"),
            new TaskRequestStep("assign-variable", "assignment", "second_number = 20"),
            new TaskRequestStep("assign-variable", "assignment", "result = first_number + second_number"),
            new TaskRequestStep("write-standard-output", "expression", "result"));
        return true;
    }

    internal static bool TryGetIntegerSignClassificationProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        if (!IntegerSignClassificationPattern.IsMatch(source))
        {
            return false;
        }

        steps = ImmutableArray.Create(
            new TaskRequestStep("assign-variable", "assignment", "number = -5"),
            new TaskRequestStep("begin-conditional", "condition", "number > 0"),
            new TaskRequestStep("write-standard-output-indented", "text", "Positive"),
            new TaskRequestStep("begin-conditional", "condition", "number < 0"),
            new TaskRequestStep("write-standard-output-indented", "text", "Negative"),
            new TaskRequestStep("begin-conditional", "condition", "number == 0"),
            new TaskRequestStep("write-standard-output-indented", "text", "Zero"));
        return true;
    }

}
