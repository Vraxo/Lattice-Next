using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class PythonFileProgramParser
{
    internal static readonly Regex DirectoryFileListingPattern = new(
        @"\b(?:accepts?|takes?)\s+(?:a|the)\s+directory\s+path\s+and\s+reports?\s+(?:the\s+)?names?\s+and\s+sizes?\s+of\s+(?:all\s+)?files?\s+(?:directly\s+)?inside\s+it,?\s+handling\s+(?:a\s+)?missing\s+directory\s+cleanly\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex NonEmptyFileLineCountPattern = new(
        @"\breads?\s+(?:a|the)\s+text\s+file\s+and\s+counts?\s+how\s+many\s+non[- ]empty\s+lines?\s+it\s+contains?\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    internal static bool TryGetDirectoryFileListingProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        if (!DirectoryFileListingPattern.IsMatch(source))
        {
            return false;
        }

        steps = ImmutableArray.Create(
            new TaskRequestStep("import-module", "module-name", "os"),
            new TaskRequestStep("import-module", "module-name", "sys"),
            new TaskRequestStep("assign-variable", "assignment", "directory = sys.argv[1]"),
            new TaskRequestStep("begin-try", "statement", "try"),
            new TaskRequestStep("begin-loop-indented", "loop-header", "entry in sorted(os.scandir(directory), key=lambda entry: entry.name)"),
            new TaskRequestStep("begin-conditional-double-indented", "condition", "entry.is_file()"),
            new TaskRequestStep("write-standard-output-triple-indented-expression", "expression", "f\"{entry.name}: {entry.stat().st_size} bytes\""),
            new TaskRequestStep("begin-except", "exception-type", "FileNotFoundError"),
            new TaskRequestStep("write-standard-output-indented-expression", "expression", "f\"Directory not found: {directory}\""));
        return true;
    }

    internal static bool TryGetNonEmptyFileLineCountProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        if (!NonEmptyFileLineCountPattern.IsMatch(source))
        {
            return false;
        }

        steps = ImmutableArray.Create(
            new TaskRequestStep("assign-variable", "assignment", "line_count = 0"),
            new TaskRequestStep("begin-with", "expression", "open(\"input.txt\", \"r\", encoding=\"utf-8\") as input_file"),
            new TaskRequestStep("begin-loop-indented", "loop-header", "line in input_file"),
            new TaskRequestStep("begin-conditional-double-indented", "condition", "line.strip()"),
            new TaskRequestStep("assign-variable-triple-indented", "assignment", "line_count += 1"),
            new TaskRequestStep("write-standard-output", "expression", "line_count"));
        return true;
    }

    internal static bool IsDirectoryListingRequest(string source) =>
        DirectoryFileListingPattern.IsMatch(source) &&
        Regex.IsMatch(source, @"\\bPython\\s+program\\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
}
