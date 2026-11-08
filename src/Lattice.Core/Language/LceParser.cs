using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

public sealed class LceParser
{
    private static readonly Regex UniversalRulePattern = new(
        @"^Every\s+(?<subtype>[a-z][a-z0-9-]*)\s+is\s+(?:a|an)\s+(?<supertype>[a-z][a-z0-9-]*)\.$",
        RegexOptions.CultureInvariant);

    private static readonly Regex AssertionPattern = new(
        @"^(?<individual>(?!It\s)(?:[A-Z][a-zA-Z0-9-]*|the\s+(?:[a-z][a-z0-9-]*\s+)*[a-z][a-z0-9-]*))\s+is\s+(?:a|an)\s+(?<concept>[a-z][a-z0-9-]*)\.$",
        RegexOptions.CultureInvariant);

    private static readonly Regex PronounAssertionPattern = new(
        @"^It\s+is\s+(?:a|an)\s+(?<concept>[a-z][a-z0-9-]*)\.$",
        RegexOptions.CultureInvariant);

    private static readonly Regex RelationAssertionPattern = new(
        @"^(?<individual>(?!It\s)(?:[A-Z][a-zA-Z0-9-]*|the\s+(?:[a-z][a-z0-9-]*\s+)*[a-z][a-z0-9-]*))\s+(?<predicate>(?!is\b)[a-z][a-z0-9-]*s)\s+(?<object>[A-Z][a-zA-Z0-9-]*|the\s+(?:[a-z][a-z0-9-]*\s+)*[a-z][a-z0-9-]*|[a-z][a-z0-9-]*)\.$",
        RegexOptions.CultureInvariant);

    private static readonly Regex QuestionPattern = new(
        @"^Is\s+(?<individual>(?!It\s)(?:[A-Z][a-zA-Z0-9-]*|the\s+(?:[a-z][a-z0-9-]*\s+)*[a-z][a-z0-9-]*))\s+(?:a|an)\s+(?<concept>[a-z][a-z0-9-]*)\?$",
        RegexOptions.CultureInvariant);

    private static readonly Regex PronounQuestionPattern = new(
        @"^Is\s+it\s+(?:a|an)\s+(?<concept>[a-z][a-z0-9-]*)\?$",
        RegexOptions.CultureInvariant);

    private static readonly Regex RelationQuestionPattern = new(
        @"^Does\s+(?<individual>(?!It\s)(?:[A-Z][a-zA-Z0-9-]*|the\s+(?:[a-z][a-z0-9-]*\s+)*[a-z][a-z0-9-]*))\s+(?<predicate>(?!be\b)[a-z][a-z0-9-]*)\s+(?<object>[A-Z][a-zA-Z0-9-]*|the\s+(?:[a-z][a-z0-9-]*\s+)*[a-z][a-z0-9-]*|[a-z][a-z0-9-]*)\?$",
        RegexOptions.CultureInvariant);

    public ParseResult Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return ParseResult.Failure("Input is empty.");
        }

        var originalSource = input.Trim();
        var source = InputNormalizer.Normalize(originalSource).Trim();

        if (source.Length == 0)
        {
            return ParseResult.Failure("Input is empty.");
        }

        var universalMatch = UniversalRulePattern.Match(source);

        if (universalMatch.Success)
        {
            var variable = new VariableTerm(new SemanticIdentifier("x"));
            var subtype = CreatePredicate(universalMatch.Groups["subtype"].Value, variable);
            var supertype = CreatePredicate(universalMatch.Groups["supertype"].Value, variable);
            var implication = new Implication(subtype, supertype);
            var meaning = new UniversalQuantification(variable, implication);

            return ParseResult.Success(new SemanticUtterance(UtteranceKind.Assertion, meaning, originalSource));
        }

        var pronounAssertionMatch = PronounAssertionPattern.Match(source);

        if (pronounAssertionMatch.Success)
        {
            var meaning = CreatePredicate(
                pronounAssertionMatch.Groups["concept"].Value,
                CreateUnresolvedReference());

            return ParseResult.Success(new SemanticUtterance(UtteranceKind.Assertion, meaning, originalSource));
        }

        var assertionMatch = AssertionPattern.Match(source);

        if (assertionMatch.Success)
        {
            var meaning = CreatePredicate(
                assertionMatch.Groups["concept"].Value,
                CreateSubjectTerm(assertionMatch.Groups["individual"].Value));

            return ParseResult.Success(new SemanticUtterance(UtteranceKind.Assertion, meaning, originalSource));
        }

        var relationAssertionMatch = RelationAssertionPattern.Match(source);

        if (relationAssertionMatch.Success)
        {
            var meaning = CreateRelationPredicate(
                relationAssertionMatch.Groups["predicate"].Value,
                relationAssertionMatch.Groups["individual"].Value,
                relationAssertionMatch.Groups["object"].Value);

            return ParseResult.Success(new SemanticUtterance(UtteranceKind.Assertion, meaning, originalSource));
        }

        var pronounQuestionMatch = PronounQuestionPattern.Match(source);

        if (pronounQuestionMatch.Success)
        {
            var meaning = CreatePredicate(
                pronounQuestionMatch.Groups["concept"].Value,
                CreateUnresolvedReference());

            return ParseResult.Success(new SemanticUtterance(UtteranceKind.Question, meaning, originalSource));
        }

        var questionMatch = QuestionPattern.Match(source);

        if (questionMatch.Success)
        {
            var meaning = CreatePredicate(
                questionMatch.Groups["concept"].Value,
                CreateSubjectTerm(questionMatch.Groups["individual"].Value));

            return ParseResult.Success(new SemanticUtterance(UtteranceKind.Question, meaning, originalSource));
        }

        var relationQuestionMatch = RelationQuestionPattern.Match(source);

        if (relationQuestionMatch.Success)
        {
            var meaning = CreateRelationPredicate(
                InflectThirdPersonSingular(relationQuestionMatch.Groups["predicate"].Value),
                relationQuestionMatch.Groups["individual"].Value,
                relationQuestionMatch.Groups["object"].Value);

            return ParseResult.Success(new SemanticUtterance(UtteranceKind.Question, meaning, originalSource));
        }

        return ParseResult.Failure(
            "That sentence is outside LCE 0.5. Use :help to see the supported forms.");
    }

    public TextParseResult ParseText(string? input)
    {
        if (string.IsNullOrWhiteSpace(input) ||
            string.IsNullOrWhiteSpace(InputNormalizer.Normalize(input)))
        {
            return TextParseResult.Failure("Input is empty.");
        }

        var source = input.Trim();
        var utterances = ImmutableArray.CreateBuilder<SemanticUtterance>();
        var sentenceStart = 0;

        for (var index = 0; index < source.Length; index++)
        {
            if (source[index] is not ('.' or '?'))
            {
                continue;
            }

            var sentence = source[sentenceStart..(index + 1)].Trim();
            var result = Parse(sentence);

            if (!result.IsSuccess || result.Utterance is null)
            {
                return TextParseResult.Failure(
                    $"Sentence {utterances.Count + 1}: {result.Error ?? "The sentence could not be parsed."}");
            }

            utterances.Add(result.Utterance);
            sentenceStart = index + 1;

            if (sentenceStart < source.Length &&
                !InputNormalizer.IsTextSeparator(source[sentenceStart]))
            {
                return TextParseResult.Failure(
                    $"Sentence {utterances.Count} must be separated from the next sentence by whitespace.");
            }

            while (sentenceStart < source.Length &&
                   InputNormalizer.IsTextSeparator(source[sentenceStart]))
            {
                sentenceStart++;
            }
        }

        if (sentenceStart < source.Length)
        {
            return TextParseResult.Failure(
                $"Sentence {utterances.Count + 1} is incomplete. Every sentence must end with a period or question mark.");
        }

        if (utterances.Count == 0)
        {
            return TextParseResult.Failure("Input does not contain a complete sentence.");
        }

        return TextParseResult.Success(utterances.ToImmutable());
    }

    private static PredicateApplication CreatePredicate(string predicate, SemanticTerm argument) =>
        new(
            CreateIdentifier(predicate),
            ImmutableArray.Create(argument));

    private static PredicateApplication CreateRelationPredicate(
        string predicate,
        string individual,
        string objectValue) =>
        new(
            CreateIdentifier(predicate),
            ImmutableArray.Create<SemanticTerm>(
                CreateSubjectTerm(individual),
                CreateSubjectTerm(objectValue)));

    private static string InflectThirdPersonSingular(string verb)
    {
        if (verb is "have" or "do" or "go")
        {
            return verb switch
            {
                "have" => "has",
                "do" => "does",
                "go" => "goes",
                _ => verb
            };
        }

        if (verb.Length > 1 &&
            verb[^1] == 'y' &&
            verb[^2] is not ('a' or 'e' or 'i' or 'o' or 'u'))
        {
            return $"{verb[..^1]}ies";
        }

        if (verb.EndsWith("s", StringComparison.Ordinal) ||
            verb.EndsWith("x", StringComparison.Ordinal) ||
            verb.EndsWith("z", StringComparison.Ordinal) ||
            verb.EndsWith("ch", StringComparison.Ordinal) ||
            verb.EndsWith("sh", StringComparison.Ordinal) ||
            verb.EndsWith("o", StringComparison.Ordinal))
        {
            return $"{verb}es";
        }

        return $"{verb}s";
    }

    private static SemanticTerm CreateSubjectTerm(string value) =>
        new IndividualTerm(CreateNounPhraseIdentifier(value));

    private static SemanticIdentifier CreateNounPhraseIdentifier(string value)
    {
        var normalized = value.StartsWith("the ", StringComparison.Ordinal)
            ? value[4..]
            : value;

        var identifier = string.Join(
            "-",
            normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        return CreateIdentifier(identifier);
    }

    private static UnresolvedReferenceTerm CreateUnresolvedReference() =>
        new(new SemanticIdentifier("it"));

    private static SemanticIdentifier CreateIdentifier(string value) =>
        new(value.ToLowerInvariant());
}
