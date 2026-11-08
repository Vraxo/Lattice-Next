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

    private static readonly Regex QuestionPattern = new(
        @"^Is\s+(?<individual>[A-Z][a-zA-Z0-9-]*)\s+(?:a|an)\s+(?<concept>[a-z][a-z0-9-]*)\?$",
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
                new IndividualTerm(CreateIdentifier(assertionMatch.Groups["individual"].Value)));

            return ParseResult.Success(new SemanticUtterance(UtteranceKind.Assertion, meaning, source));
        }

        var questionMatch = QuestionPattern.Match(source);

        if (questionMatch.Success)
        {
            var meaning = CreatePredicate(
                questionMatch.Groups["concept"].Value,
                new IndividualTerm(CreateIdentifier(questionMatch.Groups["individual"].Value)));

            return ParseResult.Success(new SemanticUtterance(UtteranceKind.Question, meaning, source));
        }

        return ParseResult.Failure(
            "That sentence is outside LCE 0.1. Use :help to see the three supported forms.");
    }

    private static PredicateApplication CreatePredicate(string predicate, SemanticTerm argument) =>
        new(
            CreateIdentifier(predicate),
            ImmutableArray.Create(argument));

    private static SemanticIdentifier CreateIdentifier(string value) =>
        new(value.ToLowerInvariant());
}
