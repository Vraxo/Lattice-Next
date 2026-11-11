using Lattice.Core.Language;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Reasoning;

public sealed class KnowledgePackLoaderTests
{
    private readonly KnowledgePackLoader loader = new();
    private readonly ReasoningEngine engine = new();
    private readonly LceParser parser = new();

    [Fact]
    public void Loads_facts_and_rules_as_imported_claims_that_can_be_queried()
    {
        const string json = """
            {
              "formatVersion": 1,
              "id": "example-animals",
              "name": "Example animals",
              "claims": [
                "Every dog is an animal.",
                "Fido is a dog."
              ]
            }
            """;
        var knowledgeBase = new KnowledgeBase();

        var loaded = loader.TryLoadAndAdd(json, knowledgeBase, out var pack, out var error);

        Assert.True(loaded, error);
        Assert.Equal(new KnowledgePack(1, "example-animals", "Example animals", 2), pack);
        Assert.Single(knowledgeBase.Facts);
        Assert.Single(knowledgeBase.Rules);
        Assert.Equal(KnowledgeSourceKind.ImportedClaim, knowledgeBase.Facts[0].SourceKind);
        Assert.Equal("example-animals", knowledgeBase.Facts[0].Sources[0].SourceId);
        Assert.Equal("example-animals", knowledgeBase.Rules[0].Sources[0].SourceId);

        var question = parser.Parse("Is Fido an animal?");
        Assert.True(question.IsSuccess, question.Error);
        var predicate = Assert.IsType<PredicateApplication>(question.Utterance!.Meaning);
        var answer = engine.Ask(knowledgeBase, predicate);

        Assert.True(answer.IsSupported);
        Assert.Equal(ProofNodeKind.Derived, answer.Proof!.Kind);
        Assert.Equal("example-animals", answer.Proof.Sources[0].SourceId);
        Assert.Equal("example-animals", answer.Proof.Premise!.Sources[0].SourceId);
    }

    [Fact]
    public void Loads_a_quantified_relational_rule_as_an_imported_claim()
    {
        const string json = """
            {
              "formatVersion": 1,
              "id": "dog-relations",
              "name": "Dog relations",
              "claims": [
                "Every dog likes milk.",
                "Fido is a dog."
              ]
            }
            """;
        var knowledgeBase = new KnowledgeBase();

        var loaded = loader.TryLoadAndAdd(
            json,
            knowledgeBase,
            out var pack,
            out var error);

        Assert.True(loaded, error);
        Assert.Equal(2, pack!.ClaimCount);

        var question = parser.Parse("Does Fido like milk?");
        Assert.True(question.IsSuccess, question.Error);
        var query = Assert.IsType<PredicateApplication>(question.Utterance!.Meaning);
        var answer = engine.Ask(knowledgeBase, query);

        Assert.True(answer.IsSupported);
        Assert.Equal(KnowledgeSourceKind.ImportedClaim, answer.Proof!.Sources[0].Kind);
        Assert.Equal("dog-relations", answer.Proof.Sources[0].SourceId);
        Assert.Equal(KnowledgeSourceKind.ImportedClaim, answer.Proof.Premise!.Sources[0].Kind);
        Assert.Equal("dog-relations", answer.Proof.Premise.Sources[0].SourceId);
    }

    [Fact]
    public void Does_not_partially_add_a_pack_when_a_later_claim_is_invalid()
    {
        const string json = """
            {
              "formatVersion": 1,
              "id": "invalid-pack",
              "name": "Invalid pack",
              "claims": [
                "Fido is a dog.",
                "It is a mammal."
              ]
            }
            """;
        var knowledgeBase = new KnowledgeBase();

        var loaded = loader.TryLoadAndAdd(json, knowledgeBase, out var pack, out var error);

        Assert.False(loaded);
        Assert.Null(pack);
        Assert.NotNull(error);
        Assert.Empty(knowledgeBase.Facts);
        Assert.Empty(knowledgeBase.Rules);
    }

    [Fact]
    public void Does_not_add_questions_from_a_pack()
    {
        const string json = """
            {
              "formatVersion": 1,
              "id": "question-pack",
              "name": "Question pack",
              "claims": [
                "Is Fido a dog?"
              ]
            }
            """;
        var knowledgeBase = new KnowledgeBase();

        var loaded = loader.TryLoadAndAdd(json, knowledgeBase, out _, out var error);

        Assert.False(loaded);
        Assert.NotNull(error);
        Assert.Empty(knowledgeBase.Facts);
    }

    [Fact]
    public void Rejects_unknown_format_versions_and_unrecognized_fields()
    {
        const string unsupportedVersion = """
            {
              "formatVersion": 2,
              "id": "future-pack",
              "name": "Future pack",
              "claims": ["Fido is a dog."]
            }
            """;
        const string unknownField = """
            {
              "formatVersion": 1,
              "id": "unknown-field",
              "name": "Unknown field",
              "claims": ["Fido is a dog."],
              "executable": true
            }
            """;

        Assert.False(loader.TryLoadAndAdd(
            unsupportedVersion,
            new KnowledgeBase(),
            out _,
            out var versionError));
        Assert.Contains("not supported", versionError);

        Assert.False(loader.TryLoadAndAdd(
            unknownField,
            new KnowledgeBase(),
            out _,
            out var fieldError));
        Assert.Contains("exactly", fieldError);
    }

    [Fact]
    public void Merges_pack_provenance_when_a_claim_already_exists()
    {
        const string json = """
            {
              "formatVersion": 1,
              "id": "canine-facts",
              "name": "Canine facts",
              "claims": ["Fido is a dog."]
            }
            """;
        var knowledgeBase = new KnowledgeBase();
        var userClaim = parser.Parse("Fido is a dog.");
        Assert.True(userClaim.IsSuccess, userClaim.Error);
        Assert.True(knowledgeBase.TryAdd(userClaim.Utterance!, out _, out var addError), addError);

        var loaded = loader.TryLoadAndAdd(json, knowledgeBase, out _, out var error);

        Assert.True(loaded, error);
        var fact = Assert.Single(knowledgeBase.Facts);
        Assert.Equal(2, fact.Sources.Length);
        Assert.Equal(KnowledgeSourceKind.UserAssertion, fact.Sources[0].Kind);
        Assert.Equal(KnowledgeSourceKind.ImportedClaim, fact.Sources[1].Kind);
        Assert.Equal("canine-facts", fact.Sources[1].SourceId);
    }

    [Fact]
    public void Candidate_attributes_can_be_stored_as_symbolic_relations_and_exactly_retrieved()
    {
        const string json = """
            {
              "formatVersion": 1,
              "id": "candidate-facts",
              "name": "Candidate facts",
              "claims": [
                "Fernbank is a reserve.",
                "Fernbank costs six-dollars."
              ]
            }
            """;
        var knowledgeBase = new KnowledgeBase();

        var loaded = loader.TryLoadAndAdd(json, knowledgeBase, out _, out var error);

        Assert.True(loaded, error);
        var cost = Assert.Single(
            knowledgeBase.Facts,
            fact => fact.Predicate.Predicate.Value == "costs");
        Assert.Equal(2, cost.Predicate.Arguments.Length);
        var costValue = Assert.IsType<IndividualTerm>(cost.Predicate.Arguments[1]);
        Assert.Equal("six-dollars", costValue.Identifier.Value);

        var query = parser.Parse("Does Fernbank cost six-dollars?");
        Assert.True(query.IsSuccess, query.Error);
        var predicate = Assert.IsType<PredicateApplication>(query.Utterance!.Meaning);
        var answer = engine.Ask(knowledgeBase, predicate);

        Assert.Equal(TruthValue.True, answer.TruthValue);
    }

    [Fact]
    public void Candidate_money_literals_and_comparative_queries_are_not_supported_by_the_current_fact_path()
    {
        const string moneyPack = """
            {
              "formatVersion": 1,
              "id": "numeric-candidate-fact",
              "name": "Numeric candidate fact",
              "claims": [
                "Fernbank costs $6."
              ]
            }
            """;
        var knowledgeBase = new KnowledgeBase();

        var loaded = loader.TryLoadAndAdd(moneyPack, knowledgeBase, out _, out var error);

        Assert.False(loaded);
        Assert.NotNull(error);
        Assert.Empty(knowledgeBase.Facts);
        Assert.Empty(knowledgeBase.Rules);

        var comparison = parser.Parse("Does Fernbank cost less than ten-dollars?");
        Assert.False(comparison.IsSuccess);
    }

    [Fact]
    public void Rejects_malformed_json_without_changing_the_knowledge_base()
    {
        var knowledgeBase = new KnowledgeBase();

        var loaded = loader.TryLoadAndAdd("{ invalid", knowledgeBase, out _, out var error);

        Assert.False(loaded);
        Assert.Equal("The knowledge pack is not valid JSON.", error);
        Assert.Empty(knowledgeBase.Facts);
        Assert.Empty(knowledgeBase.Rules);
    }
}
