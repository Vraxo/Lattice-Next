using System.Collections.Immutable;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Lattice.Client;
using Microsoft.AspNetCore.Http.Json;

namespace Lattice.Server;

/// <summary>
/// Builds the optional HTTP host for the shared Lattice client contract.
/// </summary>
public static class LatticeServer
{
    public static WebApplication Build(
        string[] args,
        Action<WebApplicationBuilder>? configureBuilder = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        configureBuilder?.Invoke(builder);
        builder.Services.Configure<JsonOptions>(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddSingleton<LatticeSessionRegistry>();

        var app = builder.Build();
        var bearerToken = app.Configuration["LATTICE_SERVER_API_KEY"];

        app.Use(async (context, next) =>
        {
            if (!string.IsNullOrWhiteSpace(bearerToken))
            {
                var supplied = context.Request.Headers.Authorization.ToString();
                var expected = $"Bearer {bearerToken}";

                if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(supplied),
                    Encoding.UTF8.GetBytes(expected)))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
            }
            else if (context.Connection.RemoteIpAddress is not { } remoteAddress ||
                     !IPAddress.IsLoopback(remoteAddress))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync(
                    "The Lattice server accepts loopback connections only unless LATTICE_SERVER_API_KEY is configured.");
                return;
            }

            await next();
        });

        app.MapPost("/api/sessions", async (
            LatticeSessionRegistry sessions,
            CancellationToken token) =>
        {
            var session = await sessions.CreateAsync(token);
            return Results.Created(
                $"/api/sessions/{session.SessionId}",
                new LatticeSessionCreated(session.SessionId));
        });

        app.MapPost(
            "/api/sessions/{sessionId:guid}/turns",
            async (Guid sessionId, LatticeTurnRequest request, LatticeSessionRegistry sessions, CancellationToken token) =>
            {
                var session = sessions.Get(sessionId);
                if (session is null)
                {
                    return (IResult)Results.NotFound();
                }

                var result = await session.SubmitAsync(request.Input, token);
                return Results.Ok(NormalizeTurn(result));
            });

        app.MapPost(
            "/api/sessions/{sessionId:guid}/knowledge-packs",
            async (Guid sessionId, LatticeKnowledgePackRequest request, LatticeSessionRegistry sessions, CancellationToken token) =>
            {
                var session = sessions.Get(sessionId);
                if (session is null)
                {
                    return (IResult)Results.NotFound();
                }

                var result = await session.LoadKnowledgePackAsync(request.Json, token);
                return Results.Ok(result);
            });

        app.MapPost(
            "/api/sessions/{sessionId:guid}/candidate-evidence",
            async (
                Guid sessionId,
                LatticeCandidateMoneyEvidence evidence,
                LatticeSessionRegistry sessions,
                CancellationToken token) =>
            {
                var session = sessions.Get(sessionId);
                if (session is null)
                {
                    return (IResult)Results.NotFound();
                }

                var result = await session.AddReviewedCandidateMoneyEvidenceAsync(evidence, token);
                return Results.Ok(result);
            });

        app.MapGet(
            "/api/sessions/{sessionId:guid}/status",
            async (Guid sessionId, LatticeSessionRegistry sessions, CancellationToken token) =>
            {
                var session = sessions.Get(sessionId);
                if (session is null)
                {
                    return (IResult)Results.NotFound();
                }

                return Results.Ok(await session.GetStatusAsync(token));
            });

        app.MapDelete(
            "/api/sessions/{sessionId:guid}",
            async (Guid sessionId, LatticeSessionRegistry sessions) =>
            {
                return await sessions.RemoveAsync(sessionId)
                    ? (IResult)Results.NoContent()
                    : Results.NotFound();
            });

        return app;
    }

    private static LatticeTurnResult NormalizeTurn(LatticeTurnResult turn)
    {
        if (turn.Utterances.IsDefaultOrEmpty)
        {
            return turn with { Utterances = ImmutableArray<LatticeUtteranceResult>.Empty };
        }

        return turn with
        {
            Utterances = turn.Utterances.Select(utterance => utterance with
            {
                SupportingEvidence = NormalizeEvidence(utterance.SupportingEvidence),
                OpposingEvidence = NormalizeEvidence(utterance.OpposingEvidence)
            }).ToImmutableArray()
        };
    }

    private static ImmutableArray<LatticeEvidence> NormalizeEvidence(
        ImmutableArray<LatticeEvidence> evidence)
    {
        if (evidence.IsDefaultOrEmpty)
        {
            return ImmutableArray<LatticeEvidence>.Empty;
        }

        return evidence.Select(item => item with
        {
            Sources = item.Sources.IsDefault
                ? ImmutableArray<LatticeEvidenceSource>.Empty
                : item.Sources,
            Premises = NormalizeEvidence(item.Premises)
        }).ToImmutableArray();
    }
}
