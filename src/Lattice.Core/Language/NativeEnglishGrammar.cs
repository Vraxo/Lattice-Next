using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

/// <summary>
/// Additional compositional English grammar for the native language parser.
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
        "something", "that", "by", "not", "no", "every", "all", "only"
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

        if (start >= end || !IsArticle(tokens[start]))
        {
            return false;
        }

        var relativeClause = FindToken(tokens, start + 1, end, "that");
        var descriptionEnd = relativeClause >= 0 ? relativeClause : end;
        var additionalProperty = (string?)null;

        if (relativeClause >= 0)
        {
            if (relativeClause + 3 != end ||
                !Is(tokens[relativeClause + 1], "is") ||
                !IsLowerWord(tokens[relativeClause + 2]))
            {
                return false;
            }

            additionalProperty = tokens[relativeClause + 2];
        }

        var firstWord = start + 1;

        if (firstWord >= descriptionEnd ||
            !Enumerable.Range(firstWord, descriptionEnd - firstWord).All(index => IsLowerWord(tokens[index])))
        {
            return false;
        }

        var descriptionWords = tokens[firstWord..descriptionEnd];
        var variable = new VariableTerm(Id("x"));
        var conditions = descriptionWords
            .Select(word => Predicate(word, variable))
            .Cast<SemanticFormula>()
            .ToList();

        if (additionalProperty is not null)
        {
            conditions.Add(Predicate(additionalProperty, variable));
        }

        meaning = new ExistentialQuantification(variable, Combine(conditions));
        return true;
    }

    private static bool TryParseQuantifiedRule(
        string[] tokens,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        if (tokens.Length == 4 && Is(tokens[0], "Only") &&
            IsPlural(tokens[1]) && Is(tokens[2], "are") &&
            IsPlural(tokens[3]))
        {
            var variable = new VariableTerm(Id("x"));
            utterance = CreateUniversalUtterance(
                variable,
                Predicate(Singularize(tokens[3]), variable),
                Predicate(Singularize(tokens[1]), variable),
                sourceText);
            return true;
        }

        if (tokens.Length == 4 && Is(tokens[0], "All") && IsPlural(tokens[1]))
        {
            var variable = new VariableTerm(Id("x"));
            var subject = Singularize(tokens[1]);

            if (Is(tokens[2], "are") && IsPlural(tokens[3]))
            {
                utterance = CreateUniversalUtterance(
                    variable,
                    Predicate(subject, variable),
                    Predicate(Singularize(tokens[3]), variable),
                    sourceText);
                return true;
            }

            if (BaseRelationVerbs.Contains(tokens[2]) &&
                TryCreateIndividual(tokens[3], allowPronoun: false, out var relationObject))
            {
                utterance = CreateUniversalUtterance(
                    variable,
                    Predicate(subject, variable),
                    Predicate(
                        InflectThirdPersonSingular(tokens[2]),
                        variable,
                        relationObject!),
                    sourceText);
                return true;
            }
        }

        if (tokens.Length < 7 ||
            !Is(tokens[0], "Every") ||
            !Is(tokens[2], "that") ||
            !Is(tokens[3], "is") ||
            !IsLowerWord(tokens[1]) ||
            !IsLowerWord(tokens[4]))
        {
            return false;
        }

        var bound = new VariableTerm(Id("x"));
        var relativePremise = new Conjunction(
            ImmutableArray.Create<SemanticFormula>(
                Predicate(tokens[4], bound),
                Predicate(tokens[1], bound)));

        if (Is(tokens[5], "is") &&
            tokens.Length == 8 &&
            IsArticle(tokens[6]) &&
            IsLowerWord(tokens[7]))
        {
            utterance = CreateUniversalUtterance(
                bound,
                relativePremise,
                Predicate(tokens[7], bound),
                sourceText);
            return true;
        }

        if (tokens.Length == 7 &&
            IsThirdPersonVerb(tokens[5]) &&
            TryCreateIndividual(tokens[6], allowPronoun: false, out var fixedObject))
        {
            utterance = CreateUniversalUtterance(
                bound,
                relativePremise,
                Predicate(tokens[5], bound, fixedObject!),
                sourceText);
            return true;
        }

        return false;
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
        var patientIndex = isQuestion ? 1 : 0;

        if (tokens.Length != 5 ||
            (isQuestion
                ? !Is(tokens[0], "Is")
                : !Is(tokens[1], "is")) ||
            !PastParticiples.TryGetValue(tokens[2], out var baseVerb) ||
            !Is(tokens[3], "by") ||
            !TryCreateIndividual(tokens[patientIndex], allowPronoun: false, out var patient) ||
            !TryCreateIndividual(tokens[4], allowPronoun: true, out var agent))
        {
            return false;
        }

        utterance = new SemanticUtterance(
            isQuestion ? UtteranceKind.Question : UtteranceKind.Assertion,
            Predicate(InflectThirdPersonSingular(baseVerb), agent!, patient!),
            sourceText);
        return true;
    }

    private static bool TryParseDefinitePropertyAssertion(
        string[] tokens,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;
        var relativeClause = FindToken(tokens, 1, tokens.Length, "that");

        if (relativeClause < 0 ||
            relativeClause + 5 != tokens.Length ||
            !Is(tokens[relativeClause + 1], "is") ||
            !Is(tokens[relativeClause + 3], "is") ||
            !IsLowerWord(tokens[relativeClause + 2]) ||
            !IsLowerWord(tokens[relativeClause + 4]) ||
            !TryCreateDefiniteDescription(tokens, 0, relativeClause + 3, out var description))
        {
            return false;
        }

        utterance = new SemanticUtterance(
            UtteranceKind.Assertion,
            Predicate(tokens[relativeClause + 4], description!),
            sourceText);
        return true;
    }

    private static bool TryParseActiveRelationAssertion(
        string[] tokens,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        if (tokens.Length < 3 ||
            !IsCapitalized(tokens[0]) ||
            !IsThirdPersonVerb(tokens[1]) ||
            !TryCreateIndividual(tokens[0], allowPronoun: true, out var subject) ||
            !TryCreateObjectTerm(tokens, 2, tokens.Length, out var relationObject) ||
            HasMultipleUnresolvedReferences(subject, relationObject))
        {
            return false;
        }

        utterance = new SemanticUtterance(
            UtteranceKind.Assertion,
            Predicate(tokens[1], subject!, relationObject!),
            sourceText);
        return true;
    }

    private static bool TryParseActiveRelationQuestion(
        string[] tokens,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        if (tokens.Length < 4 ||
            !Is(tokens[0], "Does") ||
            IsArticle(tokens[1]) ||
            !TryCreateIndividual(tokens[1], allowPronoun: true, out var subject) ||
            !IsLowerWord(tokens[2]) ||
            Is(tokens[2], "be") ||
            Is(tokens[2], "exist") ||
            !TryCreateObjectTerm(tokens, 3, tokens.Length, out var relationObject) ||
            HasMultipleUnresolvedReferences(subject, relationObject))
        {
            return false;
        }

        utterance = new SemanticUtterance(
            UtteranceKind.Question,
            Predicate(InflectThirdPersonSingular(tokens[2]), subject!, relationObject!),
            sourceText);
        return true;
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

        if (count > 1 &&
            Is(tokens[start], "the") &&
            TryCreateDefiniteDescription(tokens, start, count, out var description))
        {
            term = description;
            return true;
        }

        return false;
    }

    private static bool TryCreateDefiniteDescription(
        string[] tokens,
        int start,
        int count,
        out DescriptiveIndividualTerm? description)
    {
        description = null;
        var end = start + count;

        if (count < 2 || !Is(tokens[start], "the"))
        {
            return false;
        }

        var relativeClause = FindToken(tokens, start + 1, end, "that");
        var descriptionEnd = relativeClause >= 0 ? relativeClause : end;
        var relativeModifier = (string?)null;

        if (relativeClause >= 0)
        {
            if (relativeClause + 3 != end ||
                !Is(tokens[relativeClause + 1], "is") ||
                !IsLowerWord(tokens[relativeClause + 2]))
            {
                return false;
            }

            relativeModifier = tokens[relativeClause + 2];
        }

        var firstWord = start + 1;

        if (firstWord >= descriptionEnd ||
            !Enumerable.Range(firstWord, descriptionEnd - firstWord).All(index => IsLowerWord(tokens[index])))
        {
            return false;
        }

        var words = tokens[firstWord..descriptionEnd];
        var head = words[^1];
        var modifiers = words[..^1]
            .Select(Id)
            .ToImmutableArray();

        if (relativeModifier is not null)
        {
            modifiers = modifiers.Add(Id(relativeModifier));
        }

        description = new DescriptiveIndividualTerm(Id(head), modifiers);
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

    private static int FindToken(string[] tokens, int start, int end, string value)
    {
        for (var index = start; index < end; index++)
        {
            if (Is(tokens[index], value))
            {
                return index;
            }
        }

        return -1;
    }

    private static SemanticIdentifier Id(string value) =>
        new(value.ToLowerInvariant());
}
