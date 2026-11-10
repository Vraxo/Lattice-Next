using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class NativeEnglishRelationParser
{
    internal static bool TryParsePassiveSomethingAssertion(
        string[] tokens,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        if (tokens.Length != 6 ||
            !EnglishGrammarTokens.Is(tokens[1], "is") ||
            !EnglishGrammarTokens.Is(tokens[2], "something") ||
            !EnglishGrammarTokens.Is(tokens[3], "that") ||
            !EnglishGrammarTokens.IsCapitalized(tokens[4]) ||
            !EnglishMorphology.IsThirdPersonVerb(tokens[5]) ||
            !EnglishDescriptionParser.TryCreateIndividual(tokens[0], allowPronoun: false, out var patient))
        {
            return false;
        }

        utterance = new SemanticUtterance(
            UtteranceKind.Assertion,
            EnglishSemanticFactory.Predicate(tokens[5], new IndividualTerm(EnglishGrammarTokens.CreateIdentifier(tokens[4])), patient!),
            sourceText);
        return true;
    }

    internal static bool TryParsePassiveRelation(
        string[] tokens,
        string sourceText,
        bool isQuestion,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        if (isQuestion && !EnglishGrammarTokens.Is(tokens[0], "Is"))
        {
            return false;
        }

        var firstParticipleIndex = isQuestion ? 2 : 2;

        for (var participleIndex = firstParticipleIndex;
             participleIndex < tokens.Length - 2;
             participleIndex++)
        {
            if (!EnglishRelationLexicon.TryGetPastParticiple(tokens[participleIndex], out var baseVerb) ||
                !EnglishGrammarTokens.Is(tokens[participleIndex + 1], "by"))
            {
                continue;
            }

            var patientStart = isQuestion ? 1 : 0;
            var patientEnd = isQuestion
                ? participleIndex
                : participleIndex - 1;

            if (!isQuestion &&
                (participleIndex < 2 || !EnglishGrammarTokens.Is(tokens[participleIndex - 1], "is")))
            {
                continue;
            }

            if (!EnglishDescriptionParser.TryCreateSubjectTerm(
                    tokens,
                    patientStart,
                    patientEnd,
                    allowPronoun: false,
                    out var patient) ||
                !EnglishDescriptionParser.TryCreateSubjectTerm(
                    tokens,
                    participleIndex + 2,
                    tokens.Length,
                    allowPronoun: true,
                    out var agent) ||
                HasMultipleUnresolvedReferences(patient, agent))
            {
                continue;
            }

            utterance = new SemanticUtterance(
                isQuestion ? UtteranceKind.Question : UtteranceKind.Assertion,
                EnglishSemanticFactory.Predicate(EnglishMorphology.InflectThirdPersonSingular(baseVerb), agent!, patient!),
                sourceText);
            return true;
        }

        return false;
    }

    internal static bool TryParseNegatedActiveRelationAssertion(
        string[] tokens,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        for (var auxiliaryIndex = 1; auxiliaryIndex < tokens.Length - 3; auxiliaryIndex++)
        {
            if (!EnglishGrammarTokens.Is(tokens[auxiliaryIndex], "does") ||
                !EnglishGrammarTokens.Is(tokens[auxiliaryIndex + 1], "not") ||
                !EnglishGrammarTokens.IsLowerWord(tokens[auxiliaryIndex + 2]) ||
                !EnglishRelationLexicon.IsBaseRelationVerb(tokens[auxiliaryIndex + 2]) ||
                !EnglishDescriptionParser.TryCreateSubjectTerm(
                    tokens,
                    0,
                    auxiliaryIndex,
                    allowPronoun: true,
                    out var subject) ||
                !EnglishDescriptionParser.TryCreateObjectTerm(
                    tokens,
                    auxiliaryIndex + 3,
                    tokens.Length,
                    out var relationObject) ||
                HasMultipleUnresolvedReferences(subject, relationObject))
            {
                continue;
            }

            var predicate = EnglishSemanticFactory.Predicate(
                EnglishMorphology.InflectThirdPersonSingular(tokens[auxiliaryIndex + 2]),
                subject!,
                relationObject!);

            utterance = new SemanticUtterance(
                UtteranceKind.Assertion,
                new Negation(predicate),
                sourceText);
            return true;
        }

        return false;
    }

    internal static bool TryParseNegatedActiveRelationQuestion(
        string[] tokens,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        if (tokens.Length < 5 || !EnglishGrammarTokens.Is(tokens[0], "Does"))
        {
            return false;
        }

        for (var negationIndex = 2; negationIndex < tokens.Length - 2; negationIndex++)
        {
            var verbIndex = negationIndex + 1;

            if (!EnglishGrammarTokens.Is(tokens[negationIndex], "not") ||
                !EnglishGrammarTokens.IsLowerWord(tokens[verbIndex]) ||
                !EnglishRelationLexicon.IsBaseRelationVerb(tokens[verbIndex]) ||
                !EnglishDescriptionParser.TryCreateSubjectTerm(
                    tokens,
                    1,
                    negationIndex,
                    allowPronoun: true,
                    out var subject) ||
                !EnglishDescriptionParser.TryCreateObjectTerm(
                    tokens,
                    verbIndex + 1,
                    tokens.Length,
                    out var relationObject) ||
                HasMultipleUnresolvedReferences(subject, relationObject))
            {
                continue;
            }

            var predicate = EnglishSemanticFactory.Predicate(
                EnglishMorphology.InflectThirdPersonSingular(tokens[verbIndex]),
                subject!,
                relationObject!);

            utterance = new SemanticUtterance(
                UtteranceKind.Question,
                new Negation(predicate),
                sourceText);
            return true;
        }

        return false;
    }

    internal static bool TryParseActiveRelationAssertion(
        string[] tokens,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        for (var verbIndex = 1; verbIndex < tokens.Length - 1; verbIndex++)
        {
            if (!EnglishMorphology.IsThirdPersonVerb(tokens[verbIndex]) ||
                !EnglishDescriptionParser.TryCreateSubjectTerm(tokens, 0, verbIndex, allowPronoun: true, out var subject) ||
                !EnglishDescriptionParser.TryCreateObjectTerm(tokens, verbIndex + 1, tokens.Length, out var relationObject) ||
                HasMultipleUnresolvedReferences(subject, relationObject))
            {
                continue;
            }

            utterance = new SemanticUtterance(
                UtteranceKind.Assertion,
                EnglishSemanticFactory.Predicate(tokens[verbIndex], subject!, relationObject!),
                sourceText);
            return true;
        }

        return false;
    }

    internal static bool TryParseActiveRelationQuestion(
        string[] tokens,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        if (tokens.Length < 4 || !EnglishGrammarTokens.Is(tokens[0], "Does"))
        {
            return false;
        }

        for (var verbIndex = 2; verbIndex < tokens.Length - 1; verbIndex++)
        {
            var verb = tokens[verbIndex];

            if (!EnglishGrammarTokens.IsLowerWord(verb) ||
                EnglishGrammarTokens.IsReservedIndividualTerm(verb) ||
                EnglishGrammarTokens.Is(verb, "be") ||
                EnglishGrammarTokens.Is(verb, "exist") ||
                !EnglishDescriptionParser.TryCreateSubjectTerm(tokens, 1, verbIndex, allowPronoun: true, out var subject) ||
                !EnglishDescriptionParser.TryCreateObjectTerm(tokens, verbIndex + 1, tokens.Length, out var relationObject) ||
                HasMultipleUnresolvedReferences(subject, relationObject))
            {
                continue;
            }

            utterance = new SemanticUtterance(
                UtteranceKind.Question,
                EnglishSemanticFactory.Predicate(EnglishMorphology.InflectThirdPersonSingular(verb), subject!, relationObject!),
                sourceText);
            return true;
        }

        return false;
    }

    private static bool HasMultipleUnresolvedReferences(
        SemanticTerm? first,
        SemanticTerm? second) =>
        first is UnresolvedReferenceTerm &&
        second is UnresolvedReferenceTerm;
}
