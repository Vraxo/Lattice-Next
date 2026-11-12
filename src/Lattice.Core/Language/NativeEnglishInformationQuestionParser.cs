using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

/// <summary>
/// Parses definition-seeking questions without treating them as yes/no predicates.
/// </summary>
internal static class NativeEnglishInformationQuestionParser
{
    internal static bool TryParseDefinitionQuestion(
        string[] tokens,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        if (tokens.Length < 3 ||
            !EnglishGrammarTokens.Is(tokens[0], "what"))
        {
            return false;
        }

        var expectsPluralHead = EnglishGrammarTokens.Is(tokens[1], "are");

        if (!expectsPluralHead && !EnglishGrammarTokens.Is(tokens[1], "is"))
        {
            return false;
        }

        var descriptionStart = EnglishGrammarTokens.IsArticle(tokens[2])
            ? 3
            : 2;

        if (!EnglishDescriptionParser.TryParseDescription(
                tokens,
                descriptionStart,
                tokens.Length,
                expectsPluralHead,
                out var description))
        {
            return false;
        }

        var meaning = new DefinitionQuestionFormula(
            EnglishGrammarTokens.CreateIdentifier(description!.Head),
            description.Modifiers
                .Select(EnglishGrammarTokens.CreateIdentifier)
                .ToImmutableArray());

        utterance = new SemanticUtterance(UtteranceKind.Question, meaning, sourceText);
        return true;
    }
}
