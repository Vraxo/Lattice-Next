using System.Collections.Immutable;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Lattice.Translators.LmStudio;

public sealed class LmStudioRuntime
{
    private readonly HttpClient httpClient;
    private readonly LmStudioConfiguration configuration;
    private readonly ILmStudioProcessRunner processRunner;
    private readonly Uri serverBaseAddress;
    private readonly SemaphoreSlim operationGate = new(1, 1);

    public LmStudioRuntime(
        HttpClient httpClient,
        LmStudioConfiguration configuration,
        ILmStudioProcessRunner? processRunner = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(configuration);

        if (!configuration.TryValidate(out var error))
        {
            throw new ArgumentException(error, nameof(configuration));
        }

        this.httpClient = httpClient;
        this.configuration = configuration;
        this.processRunner = processRunner ?? new LmStudioProcessRunner();
        serverBaseAddress = configuration.GetServerBaseAddress();
    }

    public async Task<ImmutableArray<LmStudioInstalledModel>> ListInstalledModelsAsync(
        Action<string>? reportStatus = null,
        CancellationToken cancellationToken = default)
    {
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await EnsureServerReadyCoreAsync(reportStatus, cancellationToken)
                .ConfigureAwait(false);
            return await GetInstalledModelsAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            operationGate.Release();
        }
    }

    public async Task<string> EnsureModelReadyAsync(
        string modelKey,
        string? preferredIdentifier = null,
        Action<string>? reportStatus = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelKey))
        {
            throw new ArgumentException("An LM Studio model key is required.", nameof(modelKey));
        }

        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await EnsureServerReadyCoreAsync(reportStatus, cancellationToken)
                .ConfigureAwait(false);

            var loadedIdentifiers = await GetLoadedModelIdentifiersAsync(cancellationToken)
                .ConfigureAwait(false);
            var requestedIdentifier = string.IsNullOrWhiteSpace(preferredIdentifier)
                ? modelKey
                : preferredIdentifier;

            var loadedIdentifier = FindIdentifier(
                loadedIdentifiers,
                requestedIdentifier,
                modelKey);

            if (loadedIdentifier is not null)
            {
                reportStatus?.Invoke($"LM Studio model '{loadedIdentifier}' is ready.");
                return loadedIdentifier;
            }

            var installedModels = await GetInstalledModelsAsync(cancellationToken)
                .ConfigureAwait(false);
            var installedModel = installedModels.FirstOrDefault(model =>
                string.Equals(model.Key, modelKey, StringComparison.OrdinalIgnoreCase));

            if (installedModel is null)
            {
                throw new InvalidOperationException(
                    $"Model '{modelKey}' is not installed in LM Studio. Download or import it first, then run :translator setup.");
            }

            if (!installedModel.LoadedInstanceIds.IsDefaultOrEmpty)
            {
                var existingIdentifier = installedModel.LoadedInstanceIds[0];
                reportStatus?.Invoke($"LM Studio model '{installedModel.DisplayName}' is already loaded.");
                return existingIdentifier;
            }

            reportStatus?.Invoke($"Loading '{installedModel.DisplayName}' through the LM Studio CLI...");

            var loadResult = await processRunner.RunAsync(
                configuration.LmsExecutable,
                new[] { "load", modelKey, "--identifier", requestedIdentifier },
                TimeSpan.FromSeconds(configuration.ModelLoadTimeoutSeconds),
                cancellationToken).ConfigureAwait(false);

            if (loadResult.ExitCode != 0)
            {
                var detail = string.IsNullOrWhiteSpace(loadResult.StandardError)
                    ? loadResult.StandardOutput
                    : loadResult.StandardError;
                throw new InvalidOperationException(
                    $"LM Studio could not load '{modelKey}' (exit code {loadResult.ExitCode}). {detail.Trim()}");
            }

            var startedAt = Stopwatch.GetTimestamp();
            var timeout = TimeSpan.FromSeconds(configuration.ModelLoadTimeoutSeconds);

            while (Stopwatch.GetElapsedTime(startedAt) < timeout)
            {
                cancellationToken.ThrowIfCancellationRequested();

                loadedIdentifiers = await GetLoadedModelIdentifiersAsync(cancellationToken)
                    .ConfigureAwait(false);
                loadedIdentifier = FindIdentifier(
                    loadedIdentifiers,
                    requestedIdentifier,
                    modelKey);

                if (loadedIdentifier is not null)
                {
                    reportStatus?.Invoke($"LM Studio model '{loadedIdentifier}' is ready.");
                    return loadedIdentifier;
                }

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken)
                    .ConfigureAwait(false);
            }

            throw new TimeoutException(
                $"LM Studio reported that '{modelKey}' loaded, but it did not appear in /v1/models within {configuration.ModelLoadTimeoutSeconds} seconds.");
        }
        finally
        {
            operationGate.Release();
        }
    }

    private async Task EnsureServerReadyCoreAsync(
        Action<string>? reportStatus,
        CancellationToken cancellationToken)
    {
        try
        {
            using var models = await GetJsonAsync("v1/models", TimeSpan.FromSeconds(5), cancellationToken)
                .ConfigureAwait(false);
            return;
        }
        catch (HttpRequestException exception) when (IsServerUnavailable(exception))
        {
        }

        reportStatus?.Invoke("LM Studio server is not responding. Starting it...");

        if (!processRunner.TryStartDetached(
            configuration.LmsExecutable,
            new[]
            {
                "server",
                "start",
                "--port",
                serverBaseAddress.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--bind",
                "127.0.0.1"
            },
            out var startError))
        {
            throw new InvalidOperationException(
                startError ?? $"Could not start LM Studio using '{configuration.LmsExecutable}'.");
        }

        reportStatus?.Invoke("Waiting for the LM Studio server...");

        var startedAt = Stopwatch.GetTimestamp();
        var timeout = TimeSpan.FromSeconds(configuration.ServerStartupTimeoutSeconds);

        while (Stopwatch.GetElapsedTime(startedAt) < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var models = await GetJsonAsync(
                    "v1/models",
                    TimeSpan.FromSeconds(5),
                    cancellationToken).ConfigureAwait(false);
                reportStatus?.Invoke("LM Studio server is ready.");
                return;
            }
            catch (HttpRequestException exception) when (IsServerUnavailable(exception))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        throw new TimeoutException(
            $"LM Studio did not become reachable at {serverBaseAddress} within {configuration.ServerStartupTimeoutSeconds} seconds. Check that LM Studio and its CLI are installed and that the configured port is available.");
    }

    private async Task<ImmutableArray<LmStudioInstalledModel>> GetInstalledModelsAsync(
        CancellationToken cancellationToken)
    {
        using var document = await GetJsonAsync(
            "api/v1/models",
            TimeSpan.FromSeconds(15),
            cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("models", out var models) ||
            models.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("LM Studio returned an invalid installed-model catalog.");
        }

        var result = ImmutableArray.CreateBuilder<LmStudioInstalledModel>();

        foreach (var model in models.EnumerateArray())
        {
            if (!string.Equals(GetString(model, "type"), "llm", StringComparison.Ordinal))
            {
                continue;
            }

            var key = GetString(model, "key");

            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var loadedInstanceIds = ImmutableArray.CreateBuilder<string>();

            if (model.TryGetProperty("loaded_instances", out var instances) &&
                instances.ValueKind == JsonValueKind.Array)
            {
                foreach (var instance in instances.EnumerateArray())
                {
                    var identifier = instance.ValueKind == JsonValueKind.String
                        ? instance.GetString()
                        : GetString(instance, "id");

                    if (!string.IsNullOrWhiteSpace(identifier))
                    {
                        loadedInstanceIds.Add(identifier);
                    }
                }
            }

            var sizeBytes = model.TryGetProperty("size_bytes", out var size) &&
                size.TryGetInt64(out var parsedSize)
                    ? parsedSize
                    : 0;

            result.Add(new LmStudioInstalledModel(
                key,
                GetString(model, "display_name") ?? key,
                GetString(model, "params_string"),
                sizeBytes,
                loadedInstanceIds.ToImmutable()));
        }

        return result.ToImmutable();
    }

    private async Task<ImmutableArray<string>> GetLoadedModelIdentifiersAsync(
        CancellationToken cancellationToken)
    {
        using var document = await GetJsonAsync(
            "v1/models",
            TimeSpan.FromSeconds(10),
            cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("data", out var models) ||
            models.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("LM Studio returned an invalid loaded-model list.");
        }

        return models.EnumerateArray()
            .Select(model => GetString(model, "id"))
            .Where(identifier => !string.IsNullOrWhiteSpace(identifier))
            .Select(identifier => identifier!)
            .ToImmutableArray();
    }

    private async Task<JsonDocument> GetJsonAsync(
        string relativePath,
        TimeSpan requestTimeout,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, relativePath);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(requestTimeout);

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            timeout.Token).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(timeout.Token)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"LM Studio returned HTTP {(int)response.StatusCode}: {TrimDetail(body)}",
                null,
                response.StatusCode);
        }

        return JsonDocument.Parse(body);
    }

    public HttpRequestMessage CreateChatRequest(object body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return CreateRequest(
            HttpMethod.Post,
            "api/v1/chat",
            body);
    }

    public TimeSpan RequestTimeout => TimeSpan.FromSeconds(configuration.RequestTimeoutSeconds);

    public async Task<string> ReadChatResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"LM Studio returned HTTP {(int)response.StatusCode}: {TrimDetail(body)}",
                null,
                response.StatusCode);
        }

        return body;
    }

    private HttpRequestMessage CreateRequest(
        HttpMethod method,
        string relativePath,
        object? body = null)
    {
        var request = new HttpRequestMessage(
            method,
            new Uri(serverBaseAddress, relativePath));

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (!string.IsNullOrWhiteSpace(configuration.ApiToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                configuration.ApiToken);
        }

        return request;
    }

    private static string? FindIdentifier(
        ImmutableArray<string> identifiers,
        string preferredIdentifier,
        string modelKey)
    {
        var preferred = identifiers.FirstOrDefault(identifier =>
            string.Equals(identifier, preferredIdentifier, StringComparison.OrdinalIgnoreCase));

        return preferred ?? identifiers.FirstOrDefault(identifier =>
            string.Equals(identifier, modelKey, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsServerUnavailable(HttpRequestException exception) =>
        exception.StatusCode is null or
            HttpStatusCode.BadGateway or
            HttpStatusCode.ServiceUnavailable or
            HttpStatusCode.GatewayTimeout;

    private static string TrimDetail(string detail) =>
        detail.Length > 500 ? detail[..500] : detail;

    private static string? GetString(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
