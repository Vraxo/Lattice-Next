using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

/// <summary>
/// Compositional English grammar for the native language parser.
/// Sentences are mapped directly to semantic formulas; no LCE text is generated.
/// </summary>
internal static class NativeEnglishGrammar
{
    public static bool TryParse(
        string source,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        if (!EnglishGrammarTokens.TryReadSentence(source, out var tokens, out var terminal))
        {
            return false;
        }

        if (terminal == '?')
        {
            return NativeEnglishInformationQuestionParser.TryParseDefinitionQuestion(tokens, sourceText, out utterance) ||
                NativeEnglishQuantifierParser.TryParseUniversalQuestion(tokens, sourceText, out utterance) ||
                NativeEnglishQuantifierParser.TryParseExistenceQuestion(tokens, sourceText, out utterance) ||
                NativeEnglishQuantifierParser.TryParseClassMembershipQuestion(tokens, sourceText, out utterance) ||
                NativeEnglishPropertyParser.TryParseNegatedNamedPredicateQuestion(tokens, sourceText, out utterance) ||
                NativeEnglishPropertyParser.TryParseDefinitePropertyQuestion(tokens, sourceText, out utterance) ||
            NativeEnglishPropertyParser.TryParseNamedPropertyQuestion(tokens, sourceText, out utterance) ||
                NativeEnglishRelationParser.TryParsePassiveRelation(tokens, sourceText, isQuestion: true, out utterance) ||
                NativeEnglishRelationParser.TryParseNegatedActiveRelationQuestion(tokens, sourceText, out utterance) ||
                NativeEnglishRelationParser.TryParseActiveRelationQuestion(tokens, sourceText, out utterance);
        }

        return NativeEnglishQuantifierParser.TryParseExistentialAssertion(tokens, sourceText, out utterance) ||
            NativeEnglishQuantifierParser.TryParseQuantifiedRule(tokens, sourceText, out utterance) ||
            NativeEnglishQuantifierParser.TryParseClassMembershipAssertion(tokens, sourceText, out utterance) ||
            NativeEnglishPropertyParser.TryParseCoordinatedNamedPredicateAssertion(tokens, sourceText, out utterance) ||
            NativeEnglishPropertyParser.TryParseNegatedNamedPredicateAssertion(tokens, sourceText, out utterance) ||
            NativeEnglishRelationParser.TryParsePassiveSomethingAssertion(tokens, sourceText, out utterance) ||
            NativeEnglishRelationParser.TryParsePassiveRelation(tokens, sourceText, isQuestion: false, out utterance) ||
            NativeEnglishPropertyParser.TryParseDefinitePropertyAssertion(tokens, sourceText, out utterance) ||
            NativeEnglishRelationParser.TryParseNegatedActiveRelationAssertion(tokens, sourceText, out utterance) ||
            NativeEnglishRelationParser.TryParseActiveRelationAssertion(tokens, sourceText, out utterance);
    }
}
