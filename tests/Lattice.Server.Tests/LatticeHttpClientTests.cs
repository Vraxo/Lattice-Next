using Lattice.Client;
using Lattice.Client.Http;
using Lattice.Server;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace Lattice.Server.Tests;

public sealed class LatticeHttpClientTests
{
    [Fact]
    public async Task Http_client_uses_the_shared_session_contract_end_to_end()
    {
        await using var app = LatticeServer.Build(
            [],
            builder =>
            {
                builder.WebHost.UseTestServer();
                builder.Configuration["LATTICE_SERVER_API_KEY"] = "test-secret";
            });
        await app.StartAsync();

        using var http = app.GetTestClient();
        var client = new HttpLatticeClient(http, "test-secret");
        await using var session = await client.CreateSessionAsync();

        var firstTurn = await session.SubmitAsync("Fido is a dog.");
        var secondTurn = await session.SubmitAsync("Is Fido a dog?");
        var status = await session.GetStatusAsync();

        Assert.True(firstTurn.IsSuccess);
        Assert.Equal(LatticeUtteranceResultKind.StatementStored, firstTurn.Utterances[0].Kind);
        Assert.True(secondTurn.IsSuccess);
        Assert.Equal(LatticeTruthValue.True, secondTurn.Utterances[0].TruthValue);
        Assert.Equal(6, status.FactCount);
    }

    [Fact]
    public async Task Http_client_loads_knowledge_packs_and_preserves_imported_provenance()
    {
        await using var app = LatticeServer.Build(
            [],
            builder =>
            {
                builder.WebHost.UseTestServer();
                builder.Configuration["LATTICE_SERVER_API_KEY"] = "test-secret";
            });
        await app.StartAsync();

        using var http = app.GetTestClient();
        var client = new HttpLatticeClient(http, "test-secret");
        await using var session = await client.CreateSessionAsync();

        var loaded = await session.LoadKnowledgePackAsync(
            """{"formatVersion":1,"id":"animals","name":"Animals","claims":["Fido is a dog."]}""");
        var turn = await session.SubmitAsync("Is Fido a dog?");

        Assert.True(loaded.IsSuccess);
        Assert.Equal("Animals", loaded.Name);
        Assert.Equal(LatticeTruthValue.True, turn.Utterances[0].TruthValue);
        Assert.Equal(
            "animals",
            turn.Utterances[0].SupportingEvidence[0].Sources[0].SourceId);
    }

    [Fact]
    public async Task Http_client_adds_reviewed_non_monetary_candidate_attribute_with_provenance()
    {
        await using var app = LatticeServer.Build(
            [],
            builder =>
            {
                builder.WebHost.UseTestServer();
                builder.Configuration["LATTICE_SERVER_API_KEY"] = "test-secret";
            });
        await app.StartAsync();

        using var http = app.GetTestClient();
        var client = new HttpLatticeClient(http, "test-secret");
        await using var session = await client.CreateSessionAsync();
        const string pack = """
            {
              "formatVersion": 1,
              "id": "walking-place",
              "name": "Walking place",
              "claims": [],
              "attributes": [
                {
                  "subject": "Quiet Park",
                  "name": "transport-mode",
                  "value": { "type": "text", "value": "walking" },
                  "sourceText": "Quiet Park has a walking route.",
                  "requiresFreshness": true
                },
                {
                  "subject": "Quiet Park",
                  "name": "tags",
                  "value": { "type": "textSet", "values": ["quiet", "outdoors"] },
                  "sourceText": "Quiet Park is quiet and outdoors."
                }
              ]
            }
            """;
        var loaded = await session.LoadKnowledgePackAsync(pack);
        Assert.True(loaded.IsSuccess, loaded.Error);
        await session.SubmitAsync(
            "I have no car. I would prefer somewhere quiet and outdoors. Help me find a place.");

        using var valueDocument = System.Text.Json.JsonDocument.Parse("""{"type":"text","value":"walking"}""");
        var retrievedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var freshUntil = DateTimeOffset.UtcNow.AddHours(1);
        var result = await session.AddReviewedCandidateAttributeEvidenceAsync(
            new LatticeCandidateAttributeEvidence(
                "Quiet Park",
                "transport-mode",
                valueDocument.RootElement.Clone(),
                "Quiet Park has a walking-only route.",
                "https://example.org/quiet-park",
                retrievedAt)
            {
                FreshUntil = freshUntil
            });

        Assert.True(result.IsSuccess, result.Error);
        var status = await session.GetStatusAsync();
        var evaluation = Assert.IsType<LatticeObjectiveEvaluation>(status.ObjectiveEvaluation);
        var candidate = Assert.Single(evaluation.Candidates);
        Assert.Equal("Eligible", candidate.Feasibility);
        var transport = Assert.Single(candidate.Conditions, condition => condition.Name == "transport");
        Assert.Equal("Satisfied", transport.Status);
        Assert.Contains(transport.Evidence, source =>
            source.Kind == "ExternalObservation" &&
            source.SourceId == "https://example.org/quiet-park" &&
            source.RetrievedAt == retrievedAt &&
            source.FreshUntil == freshUntil &&
            source.RequiresFreshness);
    }

    [Fact]
    public async Task Server_requires_the_configured_bearer_token()
    {
        await using var app = LatticeServer.Build(
            [],
            builder =>
            {
                builder.WebHost.UseTestServer();
                builder.Configuration["LATTICE_SERVER_API_KEY"] = "test-secret";
            });
        await app.StartAsync();

        using var http = app.GetTestClient();
        using var response = await http.PostAsync("/api/sessions", null);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Disposing_a_remote_session_removes_it_from_the_server()
    {
        await using var app = LatticeServer.Build(
            [],
            builder =>
            {
                builder.WebHost.UseTestServer();
                builder.Configuration["LATTICE_SERVER_API_KEY"] = "test-secret";
            });
        await app.StartAsync();

        using var http = app.GetTestClient();
        var client = new HttpLatticeClient(http, "test-secret");
        var session = await client.CreateSessionAsync();
        var sessionId = session.SessionId;

        await session.DisposeAsync();

        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-secret");
        using var response = await http.GetAsync($"/api/sessions/{sessionId}/status");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Http_client_adds_reviewed_candidate_evidence_with_external_provenance()
    {
        await using var app = LatticeServer.Build(
            [],
            builder =>
            {
                builder.WebHost.UseTestServer();
                builder.Configuration["LATTICE_SERVER_API_KEY"] = "test-secret";
            });
        await app.StartAsync();

        using var http = app.GetTestClient();
        var client = new HttpLatticeClient(http, "test-secret");
        await using var session = await client.CreateSessionAsync();
        var loaded = await session.LoadKnowledgePackAsync(
            """
            {
              "formatVersion": 1,
              "id": "laptops",
              "name": "Laptop candidates",
              "claims": [],
              "attributes": [
                { "subject": "Aster 14", "name": "candidate-type", "value": { "type": "text", "value": "laptop" }, "sourceText": "Aster 14 is a laptop." }
              ]
            }
            """);
        Assert.True(loaded.IsSuccess, loaded.Error);
        await session.SubmitAsync("Help me compare these laptops. My budget is $1,000.");

        var retrievedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var result = await session.AddReviewedCandidateMoneyEvidenceAsync(
            new LatticeCandidateMoneyEvidence(
                "Aster 14", 900m, "$", "Aster 14 is listed at $900.",
                "https://example.org/aster-14", retrievedAt));

        Assert.True(result.IsSuccess, result.Error);
        var status = await session.GetStatusAsync();
        var evaluation = Assert.IsType<LatticeObjectiveEvaluation>(status.ObjectiveEvaluation);
        var candidate = Assert.Single(evaluation.Candidates);
        var budget = Assert.Single(candidate.Conditions, condition => condition.Name == "spending-limit");
        var source = Assert.Single(budget.Evidence);
        Assert.Equal("ExternalObservation", source.Kind);
        Assert.Equal("https://example.org/aster-14", source.SourceId);
        Assert.Equal(retrievedAt, source.RetrievedAt);
        Assert.Equal("Aster 14 is listed at $900.", source.SourceText);
    }
}
