using Lattice.Core.Context;
using Lattice.Core.Language;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Context;

public sealed class ConversationContextTests
{
    [Fact]
    public void Resolves_a_pronoun_when_exactly_one_individual_is_known()
    {
        var context = new ConversationContext();
        context.Observe(Parse("Fido is a dog."));

        var result = context.Resolve(Parse("Is it a mammal?"));

        Assert.Equal(ReferenceResolutionStatus.Resolved, result.Status);

        var utterance = Assert.IsType<SemanticUtterance>(result.Utterance);
        Assert.Equal("Is it a mammal?", utterance.SourceText);
        Assert.Equal("fido", utterance.ResolvedReferent!.Value.Value);

        var predicate = Assert.IsType<PredicateApplication>(utterance.Meaning);
        var individual = Assert.IsType<IndividualTerm>(Assert.Single(predicate.Arguments));
        Assert.Equal("fido", individual.Identifier.Value);
        Assert.Equal("ask(mammal(fido))", SemanticFormatter.Format(utterance));
    }

    [Fact]
    public void Requests_clarification_when_no_individual_has_been_mentioned()
    {
        var context = new ConversationContext();

        var result = context.Resolve(Parse("Is it a mammal?"));

        Assert.Equal(ReferenceResolutionStatus.MissingReferent, result.Status);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Utterance);
    }

    [Fact]
    public void Requests_clarification_when_multiple_individuals_are_known()
    {
        var context = new ConversationContext();
        context.Observe(Parse("Fido is a dog."));
        context.Observe(Parse("Rex is a dog."));

        var result = context.Resolve(Parse("Is it a mammal?"));

        Assert.Equal(ReferenceResolutionStatus.AmbiguousReferent, result.Status);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Utterance);
    }

    [Fact]
    public void Repeated_mentions_of_the_same_individual_do_not_create_ambiguity()
    {
        var context = new ConversationContext();
        context.Observe(Parse("Fido is a dog."));
        context.Observe(Parse("Is Fido an animal?"));
        context.Observe(Parse("Fido is a mammal."));

        Assert.Equal(1, context.KnownIndividualCount);

        var result = context.Resolve(Parse("Is it a mammal?"));

        Assert.Equal(ReferenceResolutionStatus.Resolved, result.Status);
        var predicate = Assert.IsType<PredicateApplication>(result.Utterance!.Meaning);
        var individual = Assert.IsType<IndividualTerm>(Assert.Single(predicate.Arguments));
        Assert.Equal("fido", individual.Identifier.Value);
    }

    [Fact]
    public void Observes_each_named_argument_in_a_relational_utterance()
    {
        var context = new ConversationContext();
        context.Observe(Parse("Fido likes milk."));

        Assert.Equal(2, context.KnownIndividualCount);
    }


    [Fact]
    public void Observes_a_descriptive_noun_phrase_as_one_stable_individual()
    {
        var context = new ConversationContext();
        context.Observe(Parse("The red ball is a toy."));

        Assert.Equal(1, context.KnownIndividualCount);
    }

    [Fact]
    public void Does_not_rewrite_explicit_references_and_observes_the_named_individual()
    {
        var context = new ConversationContext();
        var utterance = Parse("Is Fido a dog?");

        var result = context.Resolve(utterance);

        Assert.Equal(ReferenceResolutionStatus.NotRequired, result.Status);
        Assert.Same(utterance, result.Utterance);

        context.Observe(utterance);

        Assert.Equal(1, context.KnownIndividualCount);
    }

    [Fact]
    public void Does_not_treat_a_universal_rule_as_an_individual_mention()
    {
        var context = new ConversationContext();
        context.Observe(Parse("Every dog is an animal."));

        Assert.Equal(0, context.KnownIndividualCount);
        Assert.Equal(
            ReferenceResolutionStatus.MissingReferent,
            context.Resolve(Parse("Is it an animal?")).Status);
    }

    [Fact]
    public void An_existential_witness_does_not_become_a_named_conversation_referent()
    {
        var context = new ConversationContext();
        context.Observe(Parse("A red ball exists."));

        Assert.Equal(0, context.KnownIndividualCount);
        Assert.Equal(
            ReferenceResolutionStatus.MissingReferent,
            context.Resolve(Parse("Is it a mammal?")).Status);
    }

    [Fact]
    public void Resolved_pronoun_assertion_keeps_its_source_and_resolution_provenance()
    {
        var context = new ConversationContext();
        var knowledgeBase = new KnowledgeBase();
        context.Observe(Parse("Fido is a dog."));

        var resolution = context.Resolve(Parse("It is a mammal."));
        Assert.Equal(ReferenceResolutionStatus.Resolved, resolution.Status);

        var utterance = Assert.IsType<SemanticUtterance>(resolution.Utterance);
        Assert.Equal("It is a mammal.", utterance.SourceText);
        Assert.Equal("fido", utterance.ResolvedReferent!.Value.Value);

        var accepted = knowledgeBase.TryAdd(utterance, out _, out var error);

        Assert.True(accepted, error);

        var fact = Assert.Single(knowledgeBase.Facts);
        Assert.Equal("mammal", fact.Predicate.Predicate.Value);
        Assert.Equal("It is a mammal.", fact.SourceText);
        Assert.Equal("fido", fact.ResolvedReferent!.Value.Value);
    }

    [Fact]
    public void Processes_multiple_sentences_in_order_using_context_and_reasoning()
    {
        var parser = new LceParser();
        var textResult = parser.ParseText(
            "Fido is a dog. It is a mammal. Is it a mammal?");

        Assert.True(textResult.IsSuccess, textResult.Error);

        var context = new ConversationContext();
        var knowledgeBase = new KnowledgeBase();
        var reasoningEngine = new ReasoningEngine();
        ReasoningResult? answer = null;

        foreach (var parsedUtterance in textResult.Utterances)
        {
            var resolution = context.Resolve(parsedUtterance);
            Assert.True(resolution.IsSuccess);

            var utterance = Assert.IsType<SemanticUtterance>(resolution.Utterance);

            if (utterance.Kind == UtteranceKind.Assertion)
            {
                var added = knowledgeBase.TryAdd(utterance, out _, out var error);
                Assert.True(added, error);
                context.Observe(utterance);
                continue;
            }

            context.Observe(utterance);
            var query = Assert.IsType<PredicateApplication>(utterance.Meaning);
            answer = reasoningEngine.Ask(knowledgeBase, query);
        }

        Assert.NotNull(answer);
        Assert.True(answer.IsSupported);

        var proof = Assert.IsType<ProofNode>(answer.Proof);
        Assert.Equal("It is a mammal.", proof.SourceText);
        Assert.Equal("fido", proof.ResolvedReferent!.Value.Value);
    }

    private static SemanticUtterance Parse(string sentence)
    {
        var result = new LceParser().Parse(sentence);
        Assert.True(result.IsSuccess, result.Error);
        return Assert.IsType<SemanticUtterance>(result.Utterance);
    }
}
