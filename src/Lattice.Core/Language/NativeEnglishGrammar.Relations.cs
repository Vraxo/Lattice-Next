using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static partial class NativeEnglishGrammar
{
    private static readonly HashSet<string> BaseRelationVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "like", "love", "watch", "carry", "have", "do", "go",
        "need", "want", "eat", "chase", "admire", "trust"
    };

    private static readonly Dictionary<string, string> PastParticiples = new(StringComparer.OrdinalIgnoreCase)
    {
        ["liked"] = "like",
        ["loved"] = "love",
        ["watched"] = "watch",
        ["carried"] = "carry",
        ["needed"] = "need",
        ["used"] = "use"
    };

    private static bool TryParsePassiveSomethingAssertion(
        string[] tokens,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        if (tokens.Length != 6 ||
            !Is(tokens[1], "is") ||
            !Is(tokens[2], "something") ||
            !Is(tokens[3], "that") ||
            !IsCapitalized(tokens[4]) ||
            !IsThirdPersonVerb(tokens[5]) ||
            !TryCreateIndividual(tokens[0], allowPronoun: false, out var patient))
        {
            return false;
        }

        utterance = new SemanticUtterance(
            UtteranceKind.Assertion,
            Predicate(tokens[5], new IndividualTerm(Id(tokens[4])), patient!),
            sourceText);
        return true;
    }

    private static bool TryParsePassiveRelation(
        string[] tokens,
        string sourceText,
        bool isQuestion,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        if (isQuestion && !Is(tokens[0], "Is"))
        {
            return false;
        }

        var firstParticipleIndex = isQuestion ? 2 : 2;

        for (var participleIndex = firstParticipleIndex;
             participleIndex < tokens.Length - 2;
             participleIndex++)
        {
            if (!PastParticiples.TryGetValue(tokens[participleIndex], out var baseVerb) ||
                !Is(tokens[participleIndex + 1], "by"))
            {
                continue;
            }

            var patientStart = isQuestion ? 1 : 0;
            var patientEnd = isQuestion
                ? participleIndex
                : participleIndex - 1;

            if (!isQuestion &&
                (participleIndex < 2 || !Is(tokens[participleIndex - 1], "is")))
            {
                continue;
            }

            if (!TryCreateSubjectTerm(
                    tokens,
                    patientStart,
                    patientEnd,
                    allowPronoun: false,
                    out var patient) ||
                !TryCreateSubjectTerm(
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
                Predicate(InflectThirdPersonSingular(baseVerb), agent!, patient!),
                sourceText);
            return true;
        }

        return false;
    }

    private static bool TryParseActiveRelationAssertion(
        string[] tokens,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        for (var verbIndex = 1; verbIndex < tokens.Length - 1; verbIndex++)
        {
            if (!IsThirdPersonVerb(tokens[verbIndex]) ||
                !TryCreateSubjectTerm(tokens, 0, verbIndex, allowPronoun: true, out var subject) ||
                !TryCreateObjectTerm(tokens, verbIndex + 1, tokens.Length, out var relationObject) ||
                HasMultipleUnresolvedReferences(subject, relationObject))
            {
                continue;
            }

            utterance = new SemanticUtterance(
                UtteranceKind.Assertion,
                Predicate(tokens[verbIndex], subject!, relationObject!),
                sourceText);
            return true;
        }

        return false;
    }

    private static bool TryParseActiveRelationQuestion(
        string[] tokens,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        if (tokens.Length < 4 || !Is(tokens[0], "Does"))
        {
            return false;
        }

        for (var verbIndex = 2; verbIndex < tokens.Length - 1; verbIndex++)
        {
            var verb = tokens[verbIndex];

            if (!IsLowerWord(verb) ||
                ReservedIndividualTerms.Contains(verb) ||
                Is(verb, "be") ||
                Is(verb, "exist") ||
                !TryCreateSubjectTerm(tokens, 1, verbIndex, allowPronoun: true, out var subject) ||
                !TryCreateObjectTerm(tokens, verbIndex + 1, tokens.Length, out var relationObject) ||
                HasMultipleUnresolvedReferences(subject, relationObject))
            {
                continue;
            }

            utterance = new SemanticUtterance(
                UtteranceKind.Question,
                Predicate(InflectThirdPersonSingular(verb), subject!, relationObject!),
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
