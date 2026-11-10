using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static partial class NativeEnglishGrammar
{
    private static bool TryParseExistenceQuestion(
        string[] tokens,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        if (tokens.Length < 4 ||
            !Is(tokens[0], "Is") ||
            !Is(tokens[1], "there") ||
            !TryCreateExistentialFormula(tokens, 2, tokens.Length, out var meaning))
        {
            return false;
        }

        utterance = new SemanticUtterance(UtteranceKind.Question, meaning!, sourceText);
        return true;
    }

    private static bool TryParseExistentialAssertion(
        string[] tokens,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        if (tokens.Length < 4 ||
            !Is(tokens[0], "There") ||
            !Is(tokens[1], "is") ||
            !TryCreateExistentialFormula(tokens, 2, tokens.Length, out var meaning))
        {
            return false;
        }

        utterance = new SemanticUtterance(UtteranceKind.Assertion, meaning!, sourceText);
        return true;
    }

    private static bool TryCreateExistentialFormula(
        string[] tokens,
        int start,
        int end,
        out ExistentialQuantification? meaning)
    {
        meaning = null;

        if (start >= end || !IsArticle(tokens[start]) ||
            !TryParseDescription(tokens, start + 1, end, expectPluralHead: false, out var description))
        {
            return false;
        }

        var variable = new VariableTerm(Id("x"));
        meaning = new ExistentialQuantification(
            variable,
            description!.ToExistentialConditions(variable));
        return true;
    }

    private static bool TryParseQuantifiedRule(
        string[] tokens,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        if (tokens.Length < 4 ||
            tokens[0] is not ("Every" or "All" or "Only" or "Some"))
        {
            return false;
        }

        var quantifier = tokens[0];
        var expectsPluralHead = !Is(quantifier, "Every");
        var isExistential = Is(quantifier, "Some");
        var isOnly = Is(quantifier, "Only");

        for (var separator = 2; separator < tokens.Length - 1; separator++)
        {
            if (!TryParseDescription(
                tokens,
                1,
                separator,
                expectsPluralHead,
                out var description))
            {
                continue;
            }

            if (IsCopulaForQuantifier(tokens[separator], quantifier) &&
                TryParseClassPredicate(tokens, separator + 1, tokens.Length, out var classPredicate))
            {
                var variable = new VariableTerm(Id("x"));
                var subjectConditions = description!.ToUniversalConditions(variable);

                if (isExistential)
                {
                    var conditions = GetConjuncts(description.ToExistentialConditions(variable))
                        .Add(Predicate(classPredicate!, variable));
                    utterance = new SemanticUtterance(
                        UtteranceKind.Assertion,
                        new ExistentialQuantification(variable, Combine(conditions)),
                        sourceText);
                    return true;
                }

                if (isOnly)
                {
                    if (!description!.Modifiers.IsEmpty)
                    {
                        continue;
                    }

                    utterance = CreateUniversalUtterance(
                        variable,
                        Predicate(classPredicate!, variable),
                        subjectConditions,
                        sourceText);
                    return true;
                }

                utterance = CreateUniversalUtterance(
                    variable,
                    subjectConditions,
                    Predicate(classPredicate!, variable),
                    sourceText);
                return true;
            }

            if (isExistential || isOnly)
            {
                continue;
            }

            if (!TryParseQuantifiedRelation(
                tokens,
                separator,
                tokens.Length,
                Is(quantifier, "All"),
                out var relationPredicate,
                out var relationObject))
            {
                continue;
            }

            var bound = new VariableTerm(Id("x"));
            utterance = CreateUniversalUtterance(
                bound,
                description!.ToUniversalConditions(bound),
                Predicate(relationPredicate!, bound, relationObject!),
                sourceText);
            return true;
        }

        return false;
    }

    private static bool IsCopulaForQuantifier(string token, string quantifier) =>
        Is(quantifier, "Every")
            ? string.Equals(token, "is", StringComparison.Ordinal)
            : string.Equals(token, "are", StringComparison.Ordinal);

    private static bool TryParseClassPredicate(
        string[] tokens,
        int start,
        int end,
        out string? predicate)
    {
        predicate = null;

        if (end - start == 1 && IsLowerWord(tokens[start]) &&
            !ReservedIndividualTerms.Contains(tokens[start]))
        {
            predicate = IsPlural(tokens[start])
                ? Singularize(tokens[start])
                : tokens[start].ToLowerInvariant();
            return true;
        }

        if (end - start == 2 &&
            IsArticle(tokens[start]) &&
            IsLowerWord(tokens[start + 1]) &&
            !ReservedIndividualTerms.Contains(tokens[start + 1]) &&
            !IsPlural(tokens[start + 1]))
        {
            predicate = tokens[start + 1].ToLowerInvariant();
            return true;
        }

        return false;
    }

    private static bool TryParseQuantifiedRelation(
        string[] tokens,
        int verbIndex,
        int end,
        bool pluralSubject,
        out string? predicate,
        out SemanticTerm? relationObject)
    {
        predicate = null;
        relationObject = null;
        var verb = tokens[verbIndex];

        if (pluralSubject)
        {
            if (!BaseRelationVerbs.Contains(verb))
            {
                return false;
            }

            predicate = InflectThirdPersonSingular(verb);
        }
        else
        {
            if (!IsThirdPersonVerb(verb))
            {
                return false;
            }

            predicate = verb.ToLowerInvariant();
        }

        return TryCreateObjectTerm(tokens, verbIndex + 1, end, out relationObject);
    }

    private static bool TryParseClassMembershipAssertion(
        string[] tokens,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        if (tokens.Length != 7 ||
            !IsCapitalized(tokens[0]) ||
            !Is(tokens[1], "belongs") ||
            !Is(tokens[2], "to") ||
            !Is(tokens[3], "the") ||
            !Is(tokens[4], "class") ||
            !Is(tokens[5], "of") ||
            !IsPluralHead(tokens[6]))
        {
            return false;
        }

        utterance = new SemanticUtterance(
            UtteranceKind.Assertion,
            Predicate(Singularize(tokens[6]), new IndividualTerm(Id(tokens[0]))),
            sourceText);
        return true;
    }

    private static bool TryParseClassMembershipQuestion(
        string[] tokens,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        if (tokens.Length != 6 ||
            !Is(tokens[0], "Does") ||
            !IsCapitalized(tokens[1]) ||
            !Is(tokens[2], "count") ||
            !Is(tokens[3], "as") ||
            !IsArticle(tokens[4]) ||
            !IsLowerWord(tokens[5]))
        {
            return false;
        }

        utterance = new SemanticUtterance(
            UtteranceKind.Question,
            Predicate(tokens[5], new IndividualTerm(Id(tokens[1]))),
            sourceText);
        return true;
    }
}
