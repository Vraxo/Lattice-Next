using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

/// <summary>
/// Compositional English grammar for the native language parser.
/// Sentences are mapped directly to semantic formulas; no LCE text is generated.
/// </summary>
internal static partial class NativeEnglishGrammar
{
    public static bool TryParse(
        string source,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        if (!TryReadTokens(source, out var tokens, out var terminal))
        {
            return false;
        }

        if (terminal == '?')
        {
            return TryParseExistenceQuestion(tokens, sourceText, out utterance) ||
                TryParseClassMembershipQuestion(tokens, sourceText, out utterance) ||
                TryParseDefinitePropertyQuestion(tokens, sourceText, out utterance) ||
                TryParsePassiveRelation(tokens, sourceText, isQuestion: true, out utterance) ||
                TryParseActiveRelationQuestion(tokens, sourceText, out utterance);
        }

        return TryParseExistentialAssertion(tokens, sourceText, out utterance) ||
            TryParseQuantifiedRule(tokens, sourceText, out utterance) ||
            TryParseClassMembershipAssertion(tokens, sourceText, out utterance) ||
            TryParsePassiveSomethingAssertion(tokens, sourceText, out utterance) ||
            TryParsePassiveRelation(tokens, sourceText, isQuestion: false, out utterance) ||
            TryParseDefinitePropertyAssertion(tokens, sourceText, out utterance) ||
            TryParseActiveRelationAssertion(tokens, sourceText, out utterance);
    }

    private static bool TryReadTokens(
        string source,
        out string[] tokens,
        out char terminal)
    {
        tokens = [];
        terminal = '\0';

        if (source.Length < 2 || source[^1] is not ('.' or '?'))
        {
            return false;
        }

        terminal = source[^1];
        var body = source[..^1].Trim();

        if (body.Length == 0)
        {
            return false;
        }

        tokens = body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length > 0 && tokens.All(IsWord);
    }

}
