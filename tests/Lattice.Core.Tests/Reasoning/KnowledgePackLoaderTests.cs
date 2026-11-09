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
