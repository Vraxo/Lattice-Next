using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class ProgramExpressionRequestParser
{
    private static readonly Regex VariableExpressionProgramPattern = new(
        @"\bsets?\s+(?<variable>[A-Za-z_][A-Za-z0-9_]*)\s+to\s+(?<value>-?\d+(?:\.\d+)?)\s+and\s+(?:prints?|displays?|outputs?)\s+(?<expression>.+?)\s*[.!?]?\s*$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    internal static bool TryGetVariableProgram(
        string source,
        out ProgramSpecification? program)
    {
        program = null;
        var match = VariableExpressionProgramPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        var variable = match.Groups["variable"].Value;
        var assignedValue = match.Groups["value"].Value;
        var expressionText = match.Groups["expression"].Value.Trim().TrimEnd('.', '?', '!');

        if (!IsIdentifier(variable) ||
            !ProgramExpressionParser.TryParse(assignedValue, out var assignedExpression) ||
            !ProgramExpressionParser.TryParse(expressionText, out var outputExpression))
        {
            return false;
        }

        program = new ProgramSpecification(ImmutableArray.Create<ProgramStatement>(
            new ProgramAssignmentStatement(variable, assignedExpression!),
            new ProgramOutputStatement(outputExpression!)));
        return true;
    }

    private static bool IsIdentifier(string value) =>
        value.Length > 0 &&
        (char.IsAsciiLetter(value[0]) || value[0] == '_') &&
        value.Skip(1).All(character => char.IsAsciiLetterOrDigit(character) || character == '_');
}
