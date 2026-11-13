using Xunit;
using Lattice.Client;
using Lattice.Runtime;

namespace Lattice.Runtime.Tests;

public sealed class LatticeRuntimeSessionTests
{
    [Fact]
    public async Task Stores_assertions_and_answers_questions_with_evidence()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Fido is a dog. Is Fido a dog?");

        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(2, result.Utterances.Length);
        Assert.Equal(
            LatticeUtteranceResultKind.StatementStored,
            result.Utterances[0].Kind);
        Assert.Equal(
            LatticeUtteranceResultKind.QuestionAnswered,
            result.Utterances[1].Kind);
        Assert.Equal(
            LatticeTruthValue.True,
            result.Utterances[1].TruthValue);
        Assert.Contains(
            result.Utterances[1].SupportingEvidence,
            evidence => evidence.Sources.Any(
                source => source.SourceText == "Fido is a dog."));
    }

    [Fact]
    public async Task Keeps_knowledge_isolated_between_sessions()
    {
        var client = new LatticeRuntimeClient();
        await using var first = await client.CreateSessionAsync();
        await using var second = await client.CreateSessionAsync();

        await first.SubmitAsync("Fido is a dog.");
        var result = await second.SubmitAsync("Is Fido a dog?");

        Assert.Equal(LatticeTruthValue.Unknown, result.Utterances[0].TruthValue);
        Assert.NotEqual(first.SessionId, second.SessionId);
    }

    [Fact]
    public async Task Requests_clarification_for_a_missing_referent()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync("Is it a dog?");

        Assert.Equal(
            LatticeUtteranceResultKind.ClarificationRequired,
            result.Utterances[0].Kind);
        Assert.Equal(
            LatticeReferenceStatus.MissingReferent,
            result.Utterances[0].ReferenceStatus);
    }

    [Fact]
    public async Task Rejects_a_turn_before_mutating_session_knowledge_when_parsing_fails()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var rejected = await session.SubmitAsync(
            "Fido is a dog. asdf");
        var followUp = await session.SubmitAsync("Is Fido a dog?");

        Assert.Equal(LatticeTurnStatus.Rejected, rejected.Status);
        Assert.Equal(LatticeTruthValue.Unknown, followUp.Utterances[0].TruthValue);
    }

    [Fact]
    public async Task Rejects_submissions_after_session_disposal()
    {
        var client = new LatticeRuntimeClient();
        var session = await client.CreateSessionAsync();

        await session.DisposeAsync();
        var result = await session.SubmitAsync("Fido is a dog.");

        Assert.Equal(LatticeTurnStatus.Rejected, result.Status);
        Assert.Equal("The session has been closed.", result.Error);
    }


    [Fact]
    public async Task Loads_knowledge_packs_into_the_session_with_imported_provenance()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();
        var initialStatus = await session.GetStatusAsync();

        var loaded = await session.LoadKnowledgePackAsync(
            """{"formatVersion":1,"id":"animals","name":"Animals","claims":["Fido is a dog."]}""");
        var result = await session.SubmitAsync("Is Fido a dog?");
        var status = await session.GetStatusAsync();

        Assert.True(loaded.IsSuccess);
        Assert.Equal("Animals", loaded.Name);
        Assert.Equal(1, loaded.ClaimCount);
        Assert.Equal(LatticeTruthValue.True, result.Utterances[0].TruthValue);
        Assert.Contains(
            result.Utterances[0].SupportingEvidence,
            evidence => evidence.Sources.Any(
                source => source.Kind == LatticeKnowledgeSourceKind.ImportedClaim &&
                    source.SourceId == "animals"));
        Assert.Equal(initialStatus.FactCount + 1, status.FactCount);
        Assert.Equal(initialStatus.RuleCount, status.RuleCount);
        Assert.Equal(initialStatus.LoadedKnowledgePackCount + 1, status.LoadedKnowledgePackCount);
    }

    [Fact]
    public async Task Answers_definition_questions_from_the_default_world_model_without_imports()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync("What is a polygon?");

        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, result.Utterances[0].Kind);
        Assert.Equal(
            "A polygon is a geometric shape with a closed boundary. Every boundary has a line segment. Every line segment is straight."",
            result.Utterances[0].Message);
        Assert.Equal("ask(definition(polygon))", result.Utterances[0].Meaning);
        Assert.Equal(4, result.Utterances[0].SupportingEvidence.Length);
        Assert.All(
            result.Utterances[0].SupportingEvidence,
            evidence => Assert.Contains(
                evidence.Sources,
                source => source.Kind == LatticeKnowledgeSourceKind.ImportedClaim &&
                    source.SourceId == "core-world-model"));
    }
    [Fact]
    public async Task Realizes_a_different_concept_from_its_own_knowledge()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var loaded = await session.LoadKnowledgePackAsync(
            """{"formatVersion":1,"id":"circles","name":"Circles","claims":["Circle is a geometric-shape."]}""");
        var result = await session.SubmitAsync("What is a circle?");

        Assert.True(loaded.IsSuccess, loaded.Error);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, result.Utterances[0].Kind);
        Assert.Equal("A circle is a geometric shape.", result.Utterances[0].Message);
        Assert.Contains(
            result.Utterances[0].SupportingEvidence,
            evidence => evidence.Sources.Any(source => source.SourceId == "circles"));
    }

    [Fact]
    public async Task Reports_missing_concept_knowledge_as_unavailable()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync("What is a verb?");

        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.InformationUnavailable, result.Utterances[0].Kind);
        Assert.Equal(
            "I don't have structured knowledge about verb in the available knowledge.",
            result.Utterances[0].Message);
        Assert.Empty(result.Utterances[0].SupportingEvidence);
    }

    [Fact]
    public async Task Does_not_fall_back_to_a_broader_concept_when_specific_knowledge_is_missing()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        await session.LoadKnowledgePackAsync(
            """
            {
              "formatVersion": 1,
              "id": "geometry-concepts",
              "name": "Geometry concepts",
              "claims": [
                "Polygon is a geometric-shape.",
                "Polygon has Boundary."
              ]
            }
            """);

        var result = await session.SubmitAsync("What is a regular polygon?");

        Assert.Equal(LatticeUtteranceResultKind.InformationUnavailable, result.Utterances[0].Kind);
        Assert.Equal(
            "I don't have structured knowledge about regular polygon in the available knowledge.",
            result.Utterances[0].Message);
        Assert.Empty(result.Utterances[0].SupportingEvidence);
    }


    [Fact]
    public async Task Stops_concept_traversal_when_a_relationship_forms_a_cycle()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var loaded = await session.LoadKnowledgePackAsync(
            """
            {
              "formatVersion": 1,
              "id": "cyclic-concepts",
              "name": "Cyclic concepts",
              "claims": [
                "Loop has Boundary.",
                "Boundary has Loop."
              ]
            }
            """);

        var result = await session.SubmitAsync("What is a loop?");

        Assert.True(loaded.IsSuccess, loaded.Error);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, result.Utterances[0].Kind);
        Assert.Equal(2, result.Utterances[0].SupportingEvidence.Length);
        Assert.Equal(
            """
            The available knowledge represents loop through these connected facts and general rules:
            - has → boundary
              - has → loop
            """,
            result.Utterances[0].Message);
    }
    [Fact]
    public async Task Rejects_knowledge_pack_loading_after_session_disposal()
    {
        var client = new LatticeRuntimeClient();
        var session = await client.CreateSessionAsync();

        await session.DisposeAsync();
        var result = await session.LoadKnowledgePackAsync("{}");

        Assert.False(result.IsSuccess);
        Assert.Equal("The session has been closed.", result.Error);
    }
}
