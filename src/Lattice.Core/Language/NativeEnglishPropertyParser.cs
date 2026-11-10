using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class NativeEnglishPropertyParser
{
    internal static bool TryParseNegatedNamedPredicateAssertion(
        string[] tokens,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        if (tokens.Length is not (4 or 5) ||
            !EnglishGrammarTokens.IsCapitalized(tokens[0]) ||
            EnglishGrammarTokens.Is(tokens[0], "It") ||
            !EnglishGrammarTokens.Is(tokens[1], "is") ||
            !EnglishGrammarTokens.Is(tokens[2], "not") ||
            !EnglishDescriptionParser.TryCreateIndividual(tokens[0], allowPronoun: false, out var individual) ||
            !TryParseNegatedPredicate(tokens, 3, tokens.Length, out var predicate))
        {
            return false;
        }

        utterance = new SemanticUtterance(
            UtteranceKind.Assertion,
            new Negation(EnglishSemanticFactory.Predicate(predicate!, individual!)),
            sourceText);
        return true;
    }

    internal static bool TryParseNegatedNamedPredicateQuestion(
        string[] tokens,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        if (tokens.Length is not (4 or 5) ||
            !EnglishGrammarTokens.Is(tokens[0], "Is") ||
            !EnglishGrammarTokens.IsCapitalized(tokens[1]) ||
            EnglishGrammarTokens.Is(tokens[1], "It") ||
            !EnglishGrammarTokens.Is(tokens[2], "not") ||
            !EnglishDescriptionParser.TryCreateIndividual(tokens[1], allowPronoun: false, out var individual) ||
            !TryParseNegatedPredicate(tokens, 3, tokens.Length, out var predicate))
        {
            return false;
        }

        utterance = new SemanticUtterance(
            UtteranceKind.Question,
            new Negation(EnglishSemanticFactory.Predicate(predicate!, individual!)),
            sourceText);
        return true;
    }

    private static bool TryParseNegatedPredicate(
        string[] tokens,
        int start,
        int end,
        out string? predicate)
    {
        predicate = null;

        if (end - start == 2 &&
            EnglishGrammarTokens.IsArticle(tokens[start]) &&
            EnglishGrammarTokens.IsLowerWord(tokens[start + 1]) &&
            !EnglishGrammarTokens.IsReservedIndividualTerm(tokens[start + 1]) &&
            !EnglishMorphology.IsPlural(tokens[start + 1]))
        {
            predicate = tokens[start + 1].ToLowerInvariant();
            return true;
        }

        return end - start == 1 &&
            TryParseNamedIndividualProperty(tokens, start, end, out predicate);
    }

    internal static bool TryParseDefinitePropertyAssertion(
        string[] tokens,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        for (var copulaIndex = 2; copulaIndex < tokens.Length - 1; copulaIndex++)
        {
            if (!EnglishGrammarTokens.Is(tokens[copulaIndex], "is") ||
                !EnglishDescriptionParser.TryCreateDefiniteDescription(tokens, 0, copulaIndex, out var description) ||
                !TryParseSingleProperty(tokens, copulaIndex + 1, tokens.Length, out var property))
            {
                continue;
            }

            utterance = new SemanticUtterance(
                UtteranceKind.Assertion,
                EnglishSemanticFactory.Predicate(property!, description!),
                sourceText);
            return true;
        }

        if (tokens.Length == 3 &&
            EnglishGrammarTokens.IsCapitalized(tokens[0]) &&
            !EnglishGrammarTokens.Is(tokens[0], "It") &&
            EnglishGrammarTokens.Is(tokens[1], "is") &&
            EnglishDescriptionParser.TryCreateIndividual(
                tokens[0],
                allowPronoun: false,
                out var individual) &&
            TryParseNamedIndividualProperty(tokens, 2, 3, out var namedProperty))
        {
            utterance = new SemanticUtterance(
                UtteranceKind.Assertion,
                EnglishSemanticFactory.Predicate(namedProperty!, individual!),
                sourceText);
            return true;
        }

        return false;
    }

    internal static bool TryParseDefinitePropertyQuestion(
        string[] tokens,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        if (tokens.Length < 4 || !EnglishGrammarTokens.Is(tokens[0], "Is") ||
            !EnglishDescriptionParser.TryCreateDefiniteDescription(tokens, 1, tokens.Length - 2, out var description) ||
            !EnglishGrammarTokens.IsLowerWord(tokens[^1]) ||
            EnglishGrammarTokens.IsReservedIndividualTerm(tokens[^1]))
        {
            return false;
        }

        utterance = new SemanticUtterance(
            UtteranceKind.Question,
            EnglishSemanticFactory.Predicate(tokens[^1], description!),
            sourceText);
        return true;
    }

    internal static bool TryParseNamedPropertyQuestion(
        string[] tokens,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        if (tokens.Length != 3 ||
            !EnglishGrammarTokens.Is(tokens[0], "Is") ||
            !EnglishGrammarTokens.IsCapitalized(tokens[1]) ||
            EnglishGrammarTokens.Is(tokens[1], "It") ||
            !EnglishDescriptionParser.TryCreateIndividual(
                tokens[1],
                allowPronoun: false,
                out var individual) ||
            !TryParseNamedIndividualProperty(tokens, 2, 3, out var property))
        {
            return false;
        }

        utterance = new SemanticUtterance(
            UtteranceKind.Question,
            EnglishSemanticFactory.Predicate(property!, individual!),
            sourceText);
        return true;
    }

    private static bool TryParseNamedIndividualProperty(
        string[] tokens,
        int start,
        int end,
        out string? property)
    {
        property = null;

        if (end - start != 1 ||
            !EnglishGrammarTokens.IsLowerWord(tokens[start]) ||
            !EnglishLexicon.NamedIndividualPropertyAdjectives.Contains(tokens[start]))
        {
            return false;
        }

        property = tokens[start].ToLowerInvariant();
        return true;
    }

    private static bool TryParseSingleProperty(
        string[] tokens,
        int start,
        int end,
        out string? property)
    {
        property = null;

        if (end - start == 1 &&
            EnglishGrammarTokens.IsLowerWord(tokens[start]) &&
            !EnglishGrammarTokens.IsReservedIndividualTerm(tokens[start]))
        {
            property = tokens[start].ToLowerInvariant();
            return true;
        }

        if (end - start == 2 &&
            EnglishGrammarTokens.IsArticle(tokens[start]) &&
            EnglishGrammarTokens.IsLowerWord(tokens[start + 1]) &&
            !EnglishGrammarTokens.IsReservedIndividualTerm(tokens[start + 1]) &&
            !EnglishMorphology.IsPlural(tokens[start + 1]))
        {
            property = tokens[start + 1].ToLowerInvariant();
            return true;
        }

        return false;
    }
}
