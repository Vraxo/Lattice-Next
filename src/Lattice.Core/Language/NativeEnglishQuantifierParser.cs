using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class NativeEnglishQuantifierParser
{
    internal static bool TryParseUniversalQuestion(
        string[] tokens,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        if (tokens.Length < 4)
        {
            return false;
        }

        var pluralSubject = EnglishGrammarTokens.Is(tokens[0], "Are") &&
            EnglishGrammarTokens.Is(tokens[1], "all");
        var singularSubject = EnglishGrammarTokens.Is(tokens[0], "Is") &&
            (EnglishGrammarTokens.Is(tokens[1], "Every") ||
             EnglishGrammarTokens.Is(tokens[1], "Each"));

        if (!pluralSubject && !singularSubject)
        {
            return false;
        }

        var expectPluralHead = pluralSubject;

        for (var conclusionStart = Math.Max(3, tokens.Length - 2);
             conclusionStart < tokens.Length;
             conclusionStart++)
        {
            if (!EnglishDescriptionParser.TryParseDescription(
                    tokens,
                    2,
                    conclusionStart,
                    expectPluralHead,
                    out var description) ||
                !TryParseClassPredicate(
                    tokens,
                    conclusionStart,
                    tokens.Length,
                    out var conclusionPredicate))
            {
                continue;
            }

            var variable = new VariableTerm(EnglishGrammarTokens.CreateIdentifier("x"));
            var meaning = new UniversalQuantification(
                variable,
                new Implication(
                    description!.ToUniversalConditions(variable),
                    EnglishSemanticFactory.Predicate(conclusionPredicate!, variable)));

            utterance = new SemanticUtterance(UtteranceKind.Question, meaning, sourceText);
            return true;
        }

        return false;
    }

    internal static bool TryParseExistenceQuestion(
        string[] tokens,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        if (tokens.Length < 4 ||
            !EnglishGrammarTokens.Is(tokens[0], "Is") ||
            !EnglishGrammarTokens.Is(tokens[1], "there") ||
            !TryCreateExistentialFormula(tokens, 2, tokens.Length, out var meaning))
        {
            return false;
        }

        utterance = new SemanticUtterance(UtteranceKind.Question, meaning!, sourceText);
        return true;
    }

    internal static bool TryParseExistentialAssertion(
        string[] tokens,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        if (tokens.Length < 4 ||
            !EnglishGrammarTokens.Is(tokens[0], "There") ||
            !EnglishGrammarTokens.Is(tokens[1], "is") ||
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

        if (start >= end || !EnglishGrammarTokens.IsArticle(tokens[start]) ||
            !EnglishDescriptionParser.TryParseDescription(tokens, start + 1, end, expectPluralHead: false, out var description))
        {
            return false;
        }

        var variable = new VariableTerm(EnglishGrammarTokens.CreateIdentifier("x"));
        meaning = new ExistentialQuantification(
            variable,
            description!.ToExistentialConditions(variable));
        return true;
    }

    internal static bool TryParseQuantifiedRule(
        string[] tokens,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        if (tokens.Length < 4 ||
            tokens[0] is not ("Every" or "Each" or "All" or "Only" or "Some" or "No"))
        {
            return false;
        }

        var quantifier = tokens[0];
        var isNo = EnglishGrammarTokens.Is(quantifier, "No");
        var isExistential = EnglishGrammarTokens.Is(quantifier, "Some");
        var isOnly = EnglishGrammarTokens.Is(quantifier, "Only");

        for (var separator = 2; separator < tokens.Length - 1; separator++)
        {
            var expectsPluralHead = isNo
                ? ExpectsPluralHeadForNo(tokens[separator])
                : !EnglishGrammarTokens.Is(quantifier, "Every") &&
                    !EnglishGrammarTokens.Is(quantifier, "Each");

            if (!EnglishDescriptionParser.TryParseDescription(
                tokens,
                1,
                separator,
                expectsPluralHead,
                out var description,
                allowNegatedRelativePredicates: !isExistential && !isOnly,
                allowRelationalRelativePredicates: !isExistential && !isOnly))
            {
                continue;
            }

            var negatedClassConclusion = separator + 1 < tokens.Length &&
                EnglishGrammarTokens.Is(tokens[separator + 1], "not");
            var classPredicateStart = separator + (negatedClassConclusion ? 2 : 1);

            if (isNo && negatedClassConclusion)
            {
                continue;
            }

            if (IsCopulaForQuantifier(tokens[separator], quantifier, expectsPluralHead) &&
                TryParseClassPredicate(tokens, classPredicateStart, tokens.Length, out var classPredicate))
            {
                var variable = new VariableTerm(EnglishGrammarTokens.CreateIdentifier("x"));
                var subjectConditions = description!.ToUniversalConditions(variable);

                if (isExistential)
                {
                    var conditions = EnglishSemanticFactory.GetConjuncts(description.ToExistentialConditions(variable))
                        .Add(EnglishSemanticFactory.Predicate(classPredicate!, variable));
                    utterance = new SemanticUtterance(
                        UtteranceKind.Assertion,
                        new ExistentialQuantification(variable, EnglishSemanticFactory.Combine(conditions)),
                        sourceText);
                    return true;
                }

                if (isOnly)
                {
                    if (!description!.Modifiers.IsEmpty)
                    {
                        continue;
                    }

                    utterance = EnglishSemanticFactory.CreateUniversalUtterance(
                        variable,
                        EnglishSemanticFactory.Predicate(classPredicate!, variable),
                        subjectConditions,
                        sourceText);
                    return true;
                }

                var classConclusion = EnglishSemanticFactory.Predicate(classPredicate!, variable);
                utterance = EnglishSemanticFactory.CreateUniversalUtterance(
                    variable,
                    subjectConditions,
                    isNo || negatedClassConclusion
                        ? new Negation(classConclusion)
                        : classConclusion,
                    sourceText);
                return true;
            }

            if (isExistential || isOnly)
            {
                continue;
            }

            var negatedRelationConclusion = separator + 2 < tokens.Length &&
                EnglishGrammarTokens.Is(tokens[separator], "does") &&
                EnglishGrammarTokens.Is(tokens[separator + 1], "not");

            if (isNo && negatedRelationConclusion)
            {
                continue;
            }

            if (!TryParseQuantifiedRelation(
                tokens,
                negatedRelationConclusion ? separator + 2 : separator,
                tokens.Length,
                isNo
                    ? expectsPluralHead
                    : negatedRelationConclusion || EnglishGrammarTokens.Is(quantifier, "All"),
                out var relationPredicate,
                out var relationObject))
            {
                continue;
            }

            var bound = new VariableTerm(EnglishGrammarTokens.CreateIdentifier("x"));
            var relationConclusion = EnglishSemanticFactory.Predicate(relationPredicate!, bound, relationObject!);
            utterance = EnglishSemanticFactory.CreateUniversalUtterance(
                bound,
                description!.ToUniversalConditions(bound),
                isNo || negatedRelationConclusion
                    ? new Negation(relationConclusion)
                    : relationConclusion,
                sourceText);
            return true;
        }

        return false;
    }

    private static bool IsCopulaForQuantifier(
        string token,
        string quantifier,
        bool expectsPluralHead)
    {
        if (EnglishGrammarTokens.Is(quantifier, "No"))
        {
            return expectsPluralHead
                ? EnglishGrammarTokens.Is(token, "are")
                : EnglishGrammarTokens.Is(token, "is");
        }

        return (EnglishGrammarTokens.Is(quantifier, "Every") ||
                EnglishGrammarTokens.Is(quantifier, "Each"))
            ? EnglishGrammarTokens.Is(token, "is")
            : EnglishGrammarTokens.Is(token, "are");
    }

    private static bool ExpectsPluralHeadForNo(string followingPredicate) =>
        EnglishGrammarTokens.Is(followingPredicate, "are") ||
        EnglishRelationLexicon.IsBaseRelationVerb(followingPredicate);

    private static bool TryParseClassPredicate(
        string[] tokens,
        int start,
        int end,
        out string? predicate)
    {
        predicate = null;

        if (end - start == 1 && EnglishGrammarTokens.IsLowerWord(tokens[start]) &&
            !EnglishGrammarTokens.IsReservedIndividualTerm(tokens[start]))
        {
            predicate = EnglishMorphology.IsPlural(tokens[start])
                ? EnglishMorphology.Singularize(tokens[start])
                : tokens[start].ToLowerInvariant();
            return true;
        }

        if (end - start == 2 &&
            EnglishGrammarTokens.IsArticle(tokens[start]) &&
            EnglishGrammarTokens.IsLowerWord(tokens[start + 1]) &&
            !EnglishGrammarTokens.IsReservedIndividualTerm(tokens[start + 1]) &&
            !EnglishMorphology.IsPlural(tokens[start + 1]))
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
            if (!EnglishRelationLexicon.IsBaseRelationVerb(verb))
            {
                return false;
            }

            predicate = EnglishMorphology.InflectThirdPersonSingular(verb);
        }
        else
        {
            if (!EnglishMorphology.IsThirdPersonVerb(verb))
            {
                return false;
            }

            predicate = verb.ToLowerInvariant();
        }

        return EnglishDescriptionParser.TryCreateObjectTerm(tokens, verbIndex + 1, end, out relationObject);
    }

    internal static bool TryParseClassMembershipAssertion(
        string[] tokens,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        if (tokens.Length != 7 ||
            !EnglishGrammarTokens.IsCapitalized(tokens[0]) ||
            !EnglishGrammarTokens.Is(tokens[1], "belongs") ||
            !EnglishGrammarTokens.Is(tokens[2], "to") ||
            !EnglishGrammarTokens.Is(tokens[3], "the") ||
            !EnglishGrammarTokens.Is(tokens[4], "class") ||
            !EnglishGrammarTokens.Is(tokens[5], "of") ||
            !EnglishMorphology.IsPluralHead(tokens[6]))
        {
            return false;
        }

        utterance = new SemanticUtterance(
            UtteranceKind.Assertion,
            EnglishSemanticFactory.Predicate(EnglishMorphology.Singularize(tokens[6]), new IndividualTerm(EnglishGrammarTokens.CreateIdentifier(tokens[0]))),
            sourceText);
        return true;
    }

    internal static bool TryParseClassMembershipQuestion(
        string[] tokens,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        if (tokens.Length != 6 ||
            !EnglishGrammarTokens.Is(tokens[0], "Does") ||
            !EnglishGrammarTokens.IsCapitalized(tokens[1]) ||
            !EnglishGrammarTokens.Is(tokens[2], "count") ||
            !EnglishGrammarTokens.Is(tokens[3], "as") ||
            !EnglishGrammarTokens.IsArticle(tokens[4]) ||
            !EnglishGrammarTokens.IsLowerWord(tokens[5]))
        {
            return false;
        }

        utterance = new SemanticUtterance(
            UtteranceKind.Question,
            EnglishSemanticFactory.Predicate(tokens[5], new IndividualTerm(EnglishGrammarTokens.CreateIdentifier(tokens[1]))),
            sourceText);
        return true;
    }
}
