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
        Assert.Equal(1, status.FactCount);
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
}
