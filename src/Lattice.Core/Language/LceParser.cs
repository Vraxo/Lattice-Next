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
        @"^(?<individual>[A-Z][a-zA-Z0-9-]*)\s+is\s+(?:a|an)\s+(?<concept>[a-z][a-z0-9-]*)\.$",
        RegexOptions.CultureInvariant);

    private static readonly Regex PronounAssertionPattern = new(
        @"^It\s+is\s+(?:a|an)\s+(?<concept>[a-z][a-z0-9-]*)\.$",
        RegexOptions.CultureInvariant);

    private static readonly Regex QuestionPattern = new(
        @"^Is\s+(?<individual>[A-Z][a-zA-Z0-9-]*)\s+(?:a|an)\s+(?<concept>[a-z][a-z0-9-]*)\?$",
        RegexOptions.CultureInvariant);

    private static readonly Regex PronounQuestionPattern = new(
        @"^Is\s+it\s+(?:a|an)\s+(?<concept>[a-z][a-z0-9-]*)\?$",
        RegexOptions.CultureInvariant);

    public ParseResult Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return ParseResult.Failure("Input is empty.");
        }

        var source = input.Trim();

        var universalMatch = UniversalRulePattern.Match(source);

        if (universalMatch.Success)
        {
            var variable = new VariableTerm(new SemanticIdentifier("x"));
            var subtype = CreatePredicate(universalMatch.Groups["subtype"].Value, variable);
            var supertype = CreatePredicate(universalMatch.Groups["supertype"].Value, variable);
            var implication = new Implication(subtype, supertype);
            var meaning = new UniversalQuantification(variable, implication);

            return ParseResult.Success(new SemanticUtterance(UtteranceKind.Assertion, meaning, source));
        }

        var assertionMatch = AssertionPattern.Match(source);

        if (assertionMatch.Success)
        {
            var meaning = CreatePredicate(
                assertionMatch.Groups["concept"].Value,
                CreateSubjectTerm(assertionMatch.Groups["individual"].Value));

            return ParseResult.Success(new SemanticUtterance(UtteranceKind.Assertion, meaning, source));
        }

        var pronounAssertionMatch = PronounAssertionPattern.Match(source);

        if (pronounAssertionMatch.Success)
        {
            var meaning = CreatePredicate(
                pronounAssertionMatch.Groups["concept"].Value,
                CreateUnresolvedReference());

            return ParseResult.Success(new SemanticUtterance(UtteranceKind.Assertion, meaning, source));
        }

        var questionMatch = QuestionPattern.Match(source);

        if (questionMatch.Success)
        {
            var meaning = CreatePredicate(
                questionMatch.Groups["concept"].Value,
                CreateSubjectTerm(questionMatch.Groups["individual"].Value));

            return ParseResult.Success(new SemanticUtterance(UtteranceKind.Question, meaning, source));
        }

        var pronounQuestionMatch = PronounQuestionPattern.Match(source);

        if (pronounQuestionMatch.Success)
        {
            var meaning = CreatePredicate(
                pronounQuestionMatch.Groups["concept"].Value,
                CreateUnresolvedReference());

            return ParseResult.Success(new SemanticUtterance(UtteranceKind.Question, meaning, source));
        }

        return ParseResult.Failure(
            "That sentence is outside LCE 0.2. Use :help to see the supported forms.");
    }

    private static PredicateApplication CreatePredicate(string predicate, SemanticTerm argument) =>
        new(
            CreateIdentifier(predicate),
            ImmutableArray.Create(argument));

    private static SemanticTerm CreateSubjectTerm(string value) =>
        new IndividualTerm(CreateIdentifier(value));

    private static UnresolvedReferenceTerm CreateUnresolvedReference() =>
        new(new SemanticIdentifier("it"));

    private static SemanticIdentifier CreateIdentifier(string value) =>
        new(value.ToLowerInvariant());
}
