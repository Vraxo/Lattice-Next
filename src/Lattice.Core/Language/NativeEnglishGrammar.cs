using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

/// <summary>
/// Compositional English grammar for the native language parser.
/// Sentences are mapped directly to semantic formulas; no LCE text is generated.
/// </summary>
internal static class NativeEnglishGrammar
{
    private static readonly HashSet<string> BaseRelationVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "like", "love", "watch", "carry", "have", "do", "go",
        "need", "want", "eat", "chase", "admire", "trust"
    };

    private static readonly HashSet<string> ReservedIndividualTerms = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "there", "is", "are", "be", "exist",
        "something", "that", "by", "not", "no", "every", "all", "only", "some",
        "to", "of", "class", "count", "as"
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

    private static bool TryParseDescription(
        string[] tokens,
        int start,
        int end,
        bool expectPluralHead,
        out EnglishDescription? description)
    {
        description = null;

        if (start >= end)
        {
            return false;
        }

        var lexicalWords = new List<string>();
        var relativeModifiers = new List<string>();

        for (var index = start; index < end;)
        {
            if (Is(tokens[index], "that"))
            {
                var relativeCopula = expectPluralHead ? "are" : "is";

                if (index + 2 >= end ||
                    !Is(tokens[index + 1], relativeCopula) ||
                    !IsLowerWord(tokens[index + 2]) ||
                    ReservedIndividualTerms.Contains(tokens[index + 2]))
                {
                    return false;
                }

                relativeModifiers.Add(tokens[index + 2].ToLowerInvariant());
                index += 3;
                continue;
            }

            if (!IsLowerWord(tokens[index]) ||
                ReservedIndividualTerms.Contains(tokens[index]))
            {
                return false;
            }

            lexicalWords.Add(tokens[index].ToLowerInvariant());
            index++;
        }

        if (lexicalWords.Count == 0)
        {
            return false;
        }

        var head = lexicalWords[^1];

        if (expectPluralHead)
        {
            if (!IsPlural(head))
            {
                return false;
            }

            head = Singularize(head);
        }
        else if (IsPlural(head))
        {
            return false;
        }

        var modifiers = lexicalWords
            .Take(lexicalWords.Count - 1)
            .ToImmutableArray();

        description = new EnglishDescription(
            head,
            modifiers,
            relativeModifiers.ToImmutableArray());
        return true;
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
            !IsPlural(tokens[6]))
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

    private static bool TryCreateSubjectTerm(
        string[] tokens,
        int start,
        int end,
        bool allowPronoun,
        out SemanticTerm? term)
    {
        term = null;
        var count = end - start;

        if (count == 1)
        {
            return TryCreateIndividual(tokens[start], allowPronoun, out term);
        }

        return count > 1 &&
            TryCreateDefiniteDescription(tokens, start, count, out var description) &&
            AssignDescription(description!, out term);
    }

    private static bool TryCreateObjectTerm(
        string[] tokens,
        int start,
        int end,
        out SemanticTerm? term)
    {
        term = null;
        var count = end - start;

        if (count == 1)
        {
            return TryCreateIndividual(tokens[start], allowPronoun: true, out term);
        }

        return count > 1 &&
            TryCreateDefiniteDescription(tokens, start, count, out var description) &&
            AssignDescription(description!, out term);
    }

    private static bool AssignDescription(
        DescriptiveIndividualTerm description,
        out SemanticTerm? term)
    {
        term = description;
        return true;
    }

    private static bool TryCreateDefiniteDescription(
        string[] tokens,
        int start,
        int count,
        out DescriptiveIndividualTerm? description)
    {
        description = null;

        if (count < 2 || !Is(tokens[start], "the") ||
            !TryParseDescription(
                tokens,
                start + 1,
                start + count,
                expectPluralHead: false,
                out var parsedDescription))
        {
            return false;
        }

        description = parsedDescription!.ToDefiniteTerm();
        return true;
    }

    private static bool TryCreateIndividual(
        string value,
        bool allowPronoun,
        out SemanticTerm? term)
    {
        term = null;

        if (allowPronoun && Is(value, "it"))
        {
            term = new UnresolvedReferenceTerm(Id("it"));
            return true;
        }

        if (!IsWord(value) || ReservedIndividualTerms.Contains(value))
        {
            return false;
        }

        term = new IndividualTerm(Id(value));
        return true;
    }

    private static bool HasMultipleUnresolvedReferences(
        SemanticTerm? first,
        SemanticTerm? second) =>
        first is UnresolvedReferenceTerm &&
        second is UnresolvedReferenceTerm;

    private static SemanticUtterance CreateUniversalUtterance(
        VariableTerm variable,
        SemanticFormula premise,
        SemanticFormula conclusion,
        string sourceText) =>
        new(
            UtteranceKind.Assertion,
            new UniversalQuantification(
                variable,
                new Implication(premise, conclusion)),
            sourceText);

    private static ImmutableArray<SemanticFormula> GetConjuncts(SemanticFormula formula) =>
        formula is Conjunction conjunction
            ? conjunction.Conjuncts
            : ImmutableArray.Create(formula);

    private static SemanticFormula Combine(IReadOnlyList<SemanticFormula> conditions) =>
        conditions.Count == 1
            ? conditions[0]
            : new Conjunction(conditions.ToImmutableArray());

    private static PredicateApplication Predicate(
        string name,
        params SemanticTerm[] arguments) =>
        new(Id(name), ImmutableArray.CreateRange(arguments));

    private static string InflectThirdPersonSingular(string verb)
    {
        var normalized = verb.ToLowerInvariant();

        if (normalized is "have" or "do" or "go")
        {
            return normalized switch
            {
                "have" => "has",
                "do" => "does",
                "go" => "goes",
                _ => normalized
            };
        }

        if (normalized.Length > 1 &&
            normalized.EndsWith("y", StringComparison.Ordinal) &&
            normalized[^2] is not ('a' or 'e' or 'i' or 'o' or 'u'))
        {
            return $"{normalized[..^1]}ies";
        }

        if (normalized.EndsWith("s", StringComparison.Ordinal) ||
            normalized.EndsWith("x", StringComparison.Ordinal) ||
            normalized.EndsWith("z", StringComparison.Ordinal) ||
            normalized.EndsWith("ch", StringComparison.Ordinal) ||
            normalized.EndsWith("sh", StringComparison.Ordinal) ||
            normalized.EndsWith("o", StringComparison.Ordinal))
        {
            return $"{normalized}es";
        }

        return $"{normalized}s";
    }

    private static string Singularize(string noun)
    {
        var normalized = noun.ToLowerInvariant();

        if (normalized.Length > 3 && normalized.EndsWith("ies", StringComparison.Ordinal))
        {
            return $"{normalized[..^3]}y";
        }

        if (normalized.Length > 3 &&
            (normalized.EndsWith("ses", StringComparison.Ordinal) ||
             normalized.EndsWith("xes", StringComparison.Ordinal) ||
             normalized.EndsWith("zes", StringComparison.Ordinal) ||
             normalized.EndsWith("ches", StringComparison.Ordinal) ||
             normalized.EndsWith("shes", StringComparison.Ordinal) ||
             normalized.EndsWith("oes", StringComparison.Ordinal)))
        {
            return normalized[..^2];
        }

        return normalized.Length > 1 && normalized.EndsWith("s", StringComparison.Ordinal)
            ? normalized[..^1]
            : normalized;
    }

    private static bool IsPlural(string word) =>
        IsLowerWord(word) &&
        word.Length > 1 &&
        word.EndsWith("s", StringComparison.Ordinal) &&
        !word.EndsWith("ss", StringComparison.Ordinal);

    private static bool IsThirdPersonVerb(string word) =>
        IsLowerWord(word) &&
        word.Length > 1 &&
        word.EndsWith("s", StringComparison.Ordinal) &&
        !Is(word, "is");

    private static bool IsArticle(string word) =>
        Is(word, "a") || Is(word, "an");

    private static bool IsCapitalized(string word) =>
        word.Length > 0 &&
        word[0] is >= 'A' and <= 'Z' &&
        word.Skip(1).All(character =>
            character is >= 'a' and <= 'z' or
                >= 'A' and <= 'Z' or
                >= '0' and <= '9' or '-');

    private static bool IsLowerWord(string word) =>
        word.Length > 0 &&
        word[0] is >= 'a' and <= 'z' &&
        word.All(character =>
            character is >= 'a' and <= 'z' or
                >= '0' and <= '9' or '-');

    private static bool IsWord(string word) =>
        word.Length > 0 &&
        char.IsAsciiLetter(word[0]) &&
        word.All(character =>
            char.IsAsciiLetterOrDigit(character) || character == '-');

    private static bool Is(string actual, string expected) =>
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    private static SemanticIdentifier Id(string value) =>
        new(value.ToLowerInvariant());
}
