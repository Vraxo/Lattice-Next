using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static partial class NativeEnglishGrammar
{
    private static bool TryParseDefinitePropertyAssertion(
        string[] tokens,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        for (var copulaIndex = 2; copulaIndex < tokens.Length - 1; copulaIndex++)
        {
            if (!Is(tokens[copulaIndex], "is") ||
                !TryCreateDefiniteDescription(tokens, 0, copulaIndex, out var description) ||
                !TryParseSingleProperty(tokens, copulaIndex + 1, tokens.Length, out var property))
            {
                continue;
            }

            utterance = new SemanticUtterance(
                UtteranceKind.Assertion,
                Predicate(property!, description!),
                sourceText);
            return true;
        }

        return false;
    }

    private static bool TryParseDefinitePropertyQuestion(
        string[] tokens,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        if (tokens.Length < 4 || !Is(tokens[0], "Is") ||
            !TryCreateDefiniteDescription(tokens, 1, tokens.Length - 2, out var description) ||
            !IsLowerWord(tokens[^1]) ||
            ReservedIndividualTerms.Contains(tokens[^1]))
        {
            return false;
        }

        utterance = new SemanticUtterance(
            UtteranceKind.Question,
            Predicate(tokens[^1], description!),
            sourceText);
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
            IsLowerWord(tokens[start]) &&
            !ReservedIndividualTerms.Contains(tokens[start]))
        {
            property = tokens[start].ToLowerInvariant();
            return true;
        }

        if (end - start == 2 &&
            IsArticle(tokens[start]) &&
            IsLowerWord(tokens[start + 1]) &&
            !ReservedIndividualTerms.Contains(tokens[start + 1]) &&
            !IsPlural(tokens[start + 1]))
        {
            property = tokens[start + 1].ToLowerInvariant();
            return true;
        }

        return false;
    }
}
