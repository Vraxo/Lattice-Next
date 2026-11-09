using System.Collections.Immutable;
using Lattice.Core.Intelligence;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Intelligence;

public sealed class SemanticTranslationServiceTests
{
    private readonly SemanticUtteranceValidator validator = new();

    [Fact]
    public async Task Preserves_the_original_input_as_candidate_provenance()
    {
        const string sourceText = "Could Fido belong to the canine family?";

        var translator = new FixedSemanticsTranslator(new SemanticTranslationResponse(
            ImmutableArray.Create(
                new SemanticTranslationCandidate(
                    UtteranceKind.Question,
                    Predicate("dog", new IndividualTerm(Id("fido")))))));
        var service = new SemanticTranslationService(translator);

        var result = await service.TranslateAsync(sourceText);

        Assert.Equal(SemanticTranslationStatus.ReadyForReview, result.Status);
        var candidate = Assert.Single(result.Candidates);
        Assert.Equal(sourceText, candidate.SourceText);
        Assert.Equal("ask(dog(fido))", SemanticFormatter.Format(candidate));
        Assert.Equal(sourceText, translator.ReceivedSourceText);
    }

    [Fact]
    public async Task Keeps_multiple_valid_interpretations_ambiguous()
    {
        const string sourceText = "Fido is a pet.";

        var translator = new FixedSemanticsTranslator(new SemanticTranslationResponse(
            ImmutableArray.Create(
                new SemanticTranslationCandidate(
                    UtteranceKind.Assertion,
                    Predicate("pet", new IndividualTerm(Id("fido")))),
                new SemanticTranslationCandidate(
                    UtteranceKind.Assertion,
                    Predicate("animal", new IndividualTerm(Id("fido")))))));
        var service = new SemanticTranslationService(translator);

        var result = await service.TranslateAsync(sourceText);

        Assert.Equal(SemanticTranslationStatus.Ambiguous, result.Status);
        Assert.Equal(2, result.Candidates.Length);
        Assert.All(result.Candidates, candidate => Assert.Equal(sourceText, candidate.SourceText));
    }

    [Fact]
    public async Task Reports_when_the_translator_has_no_interpretation()
    {
        var translator = new FixedSemanticsTranslator(
            new SemanticTranslationResponse(
                ImmutableArray<SemanticTranslationCandidate>.Empty,
                "The wording is outside the translator's vocabulary."));
        var service = new SemanticTranslationService(translator);

        var result = await service.TranslateAsync("A sentence it cannot interpret.");

        Assert.Equal(SemanticTranslationStatus.NoInterpretation, result.Status);
        Assert.Empty(result.Candidates);
        Assert.Contains("outside", result.Message);
    }

    [Fact]
    public async Task Rejects_the_entire_response_when_one_candidate_is_unsupported()
    {
        var translator = new FixedSemanticsTranslator(new SemanticTranslationResponse(
            ImmutableArray.Create(
                new SemanticTranslationCandidate(
                    UtteranceKind.Assertion,
                    Predicate("dog", new IndividualTerm(Id("fido")))),
                new SemanticTranslationCandidate(
                    UtteranceKind.Question,
                    Predicate(
                        "between",
                        new IndividualTerm(Id("fido")),
                        new IndividualTerm(Id("rex")),
                        new IndividualTerm(Id("milk")))))));
        var service = new SemanticTranslationService(translator);

        var result = await service.TranslateAsync("Fido is a dog.");

        Assert.Equal(SemanticTranslationStatus.Rejected, result.Status);
        Assert.Empty(result.Candidates);
        Assert.Contains("one or two arguments", result.Message);
    }

    [Fact]
    public void Validates_the_supported_semantic_shapes()
    {
        var variable = new VariableTerm(Id("x"));
        var unaryConditions = new Conjunction(ImmutableArray.Create<SemanticFormula>(
            Predicate("red", variable),
            Predicate("ball", variable)));
        var existential = new ExistentialQuantification(variable, unaryConditions);
        var unaryRule = new UniversalQuantification(
            variable,
            new Implication(
                Predicate("dog", variable),
                Predicate("animal", variable)));
        var relationalRule = new UniversalQuantification(
            variable,
            new Implication(
                unaryConditions,
                Predicate("likes", variable, new IndividualTerm(Id("milk")))));
        var descriptiveObject = new DescriptiveIndividualTerm(
            Id("ball"),
            ImmutableArray.Create(Id("red")));
        var reference = new UnresolvedReferenceTerm(Id("it"));

        var candidates = new[]
        {
            new SemanticTranslationCandidate(
                UtteranceKind.Assertion,
                Predicate("dog", new IndividualTerm(Id("fido")))),
            new SemanticTranslationCandidate(
                UtteranceKind.Assertion,
                Predicate("likes", new IndividualTerm(Id("fido")), descriptiveObject)),
            new SemanticTranslationCandidate(
                UtteranceKind.Assertion,
                Predicate("likes", reference, new IndividualTerm(Id("milk")))),
            new SemanticTranslationCandidate(UtteranceKind.Assertion, existential),
            new SemanticTranslationCandidate(UtteranceKind.Assertion, unaryRule),
            new SemanticTranslationCandidate(UtteranceKind.Assertion, relationalRule),
            new SemanticTranslationCandidate(
                UtteranceKind.Question,
                Predicate("animal", reference)),
            new SemanticTranslationCandidate(UtteranceKind.Question, existential)
        };

        foreach (var candidate in candidates)
        {
            Assert.True(validator.TryValidate(candidate, out var error), error);
        }
    }

    [Fact]
    public void Rejects_unbound_variables_in_universal_conclusions()
    {
        var rule = new UniversalQuantification(
            new VariableTerm(Id("x")),
            new Implication(
                Predicate("dog", new VariableTerm(Id("x"))),
                Predicate("animal", new VariableTerm(Id("y")))));
        var candidate = new SemanticTranslationCandidate(UtteranceKind.Assertion, rule);

        Assert.False(validator.TryValidate(candidate, out var error));
        Assert.Contains("fixed concrete object", error);
    }

    [Fact]
    public void Rejects_multiple_unresolved_references_in_a_relation()
    {
        var relation = Predicate(
            "likes",
            new UnresolvedReferenceTerm(Id("it")),
            new UnresolvedReferenceTerm(Id("it")));
        var candidate = new SemanticTranslationCandidate(UtteranceKind.Question, relation);

        Assert.False(validator.TryValidate(candidate, out var error));
        Assert.Contains("at most one", error);
    }

    [Fact]
    public async Task Passes_the_callers_cancellation_token_to_the_translator()
    {
        using var cancellation = new CancellationTokenSource();
        var translator = new FixedSemanticsTranslator(new SemanticTranslationResponse(
            ImmutableArray<SemanticTranslationCandidate>.Empty));
        var service = new SemanticTranslationService(translator);

        await service.TranslateAsync("Try this sentence.", cancellation.Token);

        Assert.Equal(cancellation.Token, translator.ReceivedCancellationToken);
    }

    [Fact]
    public async Task Preserves_a_provider_failure_message_for_diagnostics()
    {
        var translator = new FixedSemanticsTranslator(
            new SemanticTranslationResponse(ImmutableArray<SemanticTranslationCandidate>.Empty),
            new InvalidOperationException("LM Studio model is not loaded."));
        var service = new SemanticTranslationService(translator);

        var result = await service.TranslateAsync("Fido is a dog.");

        Assert.Equal(SemanticTranslationStatus.Failed, result.Status);
        Assert.Contains("LM Studio model is not loaded.", result.Message);
        Assert.Empty(result.Candidates);
    }

    private static PredicateApplication Predicate(
        string name,
        params SemanticTerm[] arguments) =>
        new(Id(name), ImmutableArray.CreateRange(arguments));

    private static SemanticIdentifier Id(string value) => new(value);
}
