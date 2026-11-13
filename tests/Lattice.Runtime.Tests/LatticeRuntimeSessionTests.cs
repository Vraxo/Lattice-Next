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
        Assert.Equal(1, status.FactCount);
        Assert.Equal(1, status.LoadedKnowledgePackCount);
    }

    [Fact]
    public async Task Answers_definition_questions_by_composing_imported_concept_facts()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var loaded = await session.LoadKnowledgePackAsync(
            """
            {
              "formatVersion": 1,
              "id": "geometry-concepts",
              "name": "Geometry concepts",
              "claims": [
                "Polygon is a geometric-shape.",
                "Polygon is a polygon.",
                "Every polygon has a boundary.",
                "The boundary is closed.",
                "The boundary has Line-segment.",
                "Line-segment is a line-segment.",
                "The line-segment is straight."
              ]
            }
            """);

        var result = await session.SubmitAsync("What is a polygon?");

        Assert.True(loaded.IsSuccess, loaded.Error);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, result.Utterances[0].Kind);
        Assert.Equal(
            """
            The available knowledge represents polygon through these connected facts:
            - is → geometric shape
            - has → a boundary
              - is → boundary
            """,
            result.Utterances[0].Message);
        Assert.Equal("ask(definition(polygon))", result.Utterances[0].Meaning);
        Assert.Equal(3, result.Utterances[0].SupportingEvidence.Length);
        Assert.All(
            result.Utterances[0].SupportingEvidence,
            evidence => Assert.Contains(
                evidence.Sources,
                source => source.Kind == LatticeKnowledgeSourceKind.ImportedClaim &&
                    source.SourceId == "geometry-concepts"));
        var derivedEvidence = Assert.Single(
            result.Utterances[0].SupportingEvidence.Where(evidence =>
                evidence.Kind == LatticeEvidenceKind.Derived &&
                evidence.Sources.Any(source =>
                    source.SourceText == "Every polygon has a boundary.")));
        Assert.Equal(LatticeEvidencePolarity.Positive, derivedEvidence.Polarity);
        Assert.NotEmpty(derivedEvidence.Premises);
        Assert.Contains(
            derivedEvidence.Premises,
            premise => premise.Sources.Any(source =>
                source.SourceText == "Polygon is a polygon."));
        Assert.DoesNotContain(
            result.Utterances[0].SupportingEvidence,
            evidence => evidence.Sources.Any(
                source => source.SourceText.Contains("A polygon is", StringComparison.Ordinal)));
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
                "Polygon has Boundary.",
                "Boundary has Polygon."
              ]
            }
            """);

        var result = await session.SubmitAsync("What is a polygon?");

        Assert.True(loaded.IsSuccess, loaded.Error);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, result.Utterances[0].Kind);
        Assert.Equal(2, result.Utterances[0].SupportingEvidence.Length);
        Assert.Equal(
            """
            The available knowledge represents polygon through these connected facts:
            - has → boundary
              - has → polygon
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
