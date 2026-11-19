using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class ProgramCollectionIterationRequestParser
{
    private static readonly Regex ListIterationPattern = new(
        @"\b(?:print|display|output)s?\s+(?:each\s+)?(?<item>[A-Za-z_][A-Za-z0-9_]*)\s+(?:in|from)\s+(?:the\s+)?(?:list\s+)?(?<items>\[[^\]]*\])",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    internal static bool TryGetListIterationProgram(
        string source,
        out ProgramSpecification? program)
    {
        program = null;
        var match = ListIterationPattern.Match(source);
        if (!match.Success ||
            !ProgramExpressionParser.TryParseListLiteral(match.Groups["items"].Value, out var collection))
        {
            return false;
        }

        var item = match.Groups["item"].Value;
        var variable = item.EndsWith("s", StringComparison.OrdinalIgnoreCase) && item.Length > 1
            ? item[..^1]
            : item;
        if (!IsIdentifier(variable))
        {
            return false;
        }

        var body = ImmutableArray.Create<ProgramStatement>(
            new ProgramOutputStatement(new ProgramNameExpression(variable)));
        program = new ProgramSpecification(ImmutableArray.Create<ProgramStatement>(
            new ProgramForEachStatement(variable, collection!, body)));
        return true;
    }

    private static bool IsIdentifier(string value) =>
        value.Length > 0 &&
        (char.IsAsciiLetter(value[0]) || value[0] == '_') &&
        value.Skip(1).All(character => char.IsAsciiLetterOrDigit(character) || character == '_');
}
