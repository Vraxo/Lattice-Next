using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lattice.Client;

namespace Lattice.Client.Http;

/// <summary>
/// Implements the public client contract over the Lattice HTTP API.
/// The supplied HttpClient remains owned by the caller.
/// </summary>
public sealed class HttpLatticeClient(HttpClient httpClient, string? bearerToken = null) : ILatticeClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async ValueTask<ILatticeSession> CreateSessionAsync(
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, "api/sessions");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<SessionCreated>(
            JsonOptions,
            cancellationToken);

        if (created is null || created.SessionId == Guid.Empty)
        {
            throw new HttpRequestException("The Lattice server returned an invalid session response.");
        }

        return new HttpLatticeSession(httpClient, bearerToken, created.SessionId);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);

        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
        }

        return request;
    }

    private sealed record SessionCreated(Guid SessionId);

    private sealed class HttpLatticeSession(
        HttpClient httpClient,
        string? bearerToken,
        Guid sessionId) : ILatticeSession
    {
        private int isDisposed;

        public Guid SessionId { get; } = sessionId;

        public async ValueTask<LatticeTurnResult> SubmitAsync(
            string input,
            CancellationToken cancellationToken = default)
        {
            using var request = CreateRequest(
                HttpMethod.Post,
                $"api/sessions/{SessionId}/turns",
                new TurnRequest(input));
            using var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<LatticeTurnResult>(
                JsonOptions,
                cancellationToken)
                ?? throw new HttpRequestException("The Lattice server returned an empty turn response.");
        }

        public async ValueTask<LatticeKnowledgePackResult> LoadKnowledgePackAsync(
            string json,
            CancellationToken cancellationToken = default)
        {
            using var request = CreateRequest(
                HttpMethod.Post,
                $"api/sessions/{SessionId}/knowledge-packs",
                new KnowledgePackRequest(json));
            using var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<LatticeKnowledgePackResult>(
                JsonOptions,
                cancellationToken)
                ?? throw new HttpRequestException("The Lattice server returned an empty knowledge-pack response.");
        }

        public async ValueTask<LatticeCandidateEvidenceResult> AddReviewedCandidateMoneyEvidenceAsync(
            LatticeCandidateMoneyEvidence evidence,
            CancellationToken cancellationToken = default)
        {
            using var request = CreateRequest(
                HttpMethod.Post,
                $"api/sessions/{SessionId}/candidate-evidence",
                evidence);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<LatticeCandidateEvidenceResult>(
                JsonOptions,
                cancellationToken)
                ?? throw new HttpRequestException("The Lattice server returned an empty candidate-evidence response.");
        }

        public async ValueTask<LatticeSessionStatus> GetStatusAsync(
            CancellationToken cancellationToken = default)
        {
            using var request = CreateRequest(
                HttpMethod.Get,
                $"api/sessions/{SessionId}/status");
            using var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<LatticeSessionStatus>(
                JsonOptions,
                cancellationToken)
                ?? throw new HttpRequestException("The Lattice server returned an empty status response.");
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref isDisposed, 1) != 0)
            {
                return;
            }

            using var request = CreateRequest(
                HttpMethod.Delete,
                $"api/sessions/{SessionId}");
            using var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }

        private HttpRequestMessage CreateRequest<T>(
            HttpMethod method,
            string path,
            T content)
        {
            var request = CreateRequest(method, path);
            request.Content = JsonContent.Create(content, options: JsonOptions);
            return request;
        }

        private HttpRequestMessage CreateRequest(HttpMethod method, string path)
        {
            var request = new HttpRequestMessage(method, path);

            if (!string.IsNullOrWhiteSpace(bearerToken))
            {
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
            }

            return request;
        }

        private sealed record TurnRequest(string Input);
        private sealed record KnowledgePackRequest(string Json);
    }
}
