using System.Collections.Immutable;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Lattice.Core.Intelligence;
using Lattice.Core.Language;
using Lattice.Core.Semantics;

namespace Lattice.Translators.LmStudio;

public sealed class LmStudioSemanticsTranslator : ISemanticsTranslator
{
    private const int MaximumCandidates = 4;
    private const int MaximumOutputTokens = 320;

    private const string SystemPrompt = """
        You translate one ordinary-language statement or question into Lattice Controlled English (LCE) 0.9.
        Return only a JSON object with exactly these properties:
        - "candidates": an array of zero to four strings. Each string must be exactly one complete LCE sentence.
        - "message": a short string explaining an abstention, or null.
        Do not put Markdown fences around the JSON.

        LCE 0.9 is a small, literal grammar, not unrestricted English. Use only these forms:
        - "Every <one or more lowercase class words> is a <lowercase concept>."
        - "Every <class words> <third-person singular verb ending in s> <one fixed object>."
        - "A <description> exists."
        - "A <description> is <property>." or "A <description> is a <concept>."
        - "<One capitalized name> is a <concept>."
        - "The <description> is <property>."
        - "<One capitalized name> <third-person singular verb> <one object>."
        - Questions use "Is <name or definite description> a <concept>?" or
          "Does <name or definite description> <base-form verb> <one object>?",
          and "Does a <description> exist?"

        LCE words are single lowercase words or hyphenated words. Proper names are one capitalized token.
        Use only the grammar forms above. LCE has no negation, disjunction, comparisons, conditionals other
        than the stated universal rule, plural quantifiers, relational joins, or multi-sentence form.
        Do not silently drop, reverse, weaken, strengthen, or invent meaning. Do not replace a word with a
        synonym merely to force a translation. Do not resolve pronouns without an explicit unique referent
        in the input itself. If the meaning cannot be represented faithfully, return an empty candidates array.
        If the wording has multiple materially different readings, return a separate candidate for each
        defensible reading, or abstain when the readings cannot be stated reliably.
        A candidate must retain the original polarity, quantifier, predicate, arguments, and question/assertion
        distinction. Prefer abstention over guessing.
        """;

    private readonly HttpClient httpClient;
    private readonly LmStudioOptions options;
    private readonly LceParser parser;

    public LmStudioSemanticsTranslator(
        HttpClient httpClient,
        LmStudioOptions options,
        LceParser? parser = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        this.httpClient = httpClient;
        this.options = options;
        this.parser = parser ?? new LceParser();
    }

    public async ValueTask<SemanticTranslationResponse> TranslateAsync(
        string sourceText,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return new SemanticTranslationResponse(
                ImmutableArray<SemanticTranslationCandidate>.Empty,
                "Input is empty.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.RequestTimeout);

        var model = await EnsureModelAvailableAsync(timeout.Token).ConfigureAwait(false);
        using var request = CreateRequest(
            HttpMethod.Post,
            "v1/chat/completions",
            new
            {
                model,
                messages = new[]
                {
                    new { role = "system", content = SystemPrompt },
                    new { role = "user", content = sourceText }
                },
                temperature = 0.0,
                max_tokens = MaximumOutputTokens,
                response_format = new { type = "json_object" }
            });

        using var response = await httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
            .ConfigureAwait(false);
        var responseBody = await ReadSuccessfulResponseAsync(response, timeout.Token)
            .ConfigureAwait(false);

        using var completion = JsonDocument.Parse(responseBody);
        var content = completion.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new JsonException("LM Studio returned an empty assistant message.");
        }

        return ParseTranslation(content);
    }

    private async Task<string> EnsureModelAvailableAsync(CancellationToken cancellationToken)
    {
        JsonDocument modelResponse;

        try
        {
            modelResponse = await GetJsonAsync("v1/models", cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception) when (
            exception.StatusCode is null &&
            options.AutoStartServer)
        {
            await StartServerAsync(cancellationToken).ConfigureAwait(false);
            modelResponse = await GetJsonAsync("v1/models", cancellationToken).ConfigureAwait(false);
        }

        using (modelResponse)
        {
            var models = modelResponse.RootElement.GetProperty("data");

            foreach (var model in models.EnumerateArray())
            {
                var identifier = GetString(model, "id");

                if (string.Equals(identifier, options.Model, StringComparison.Ordinal))
                {
                    return identifier;
                }
            }
        }

        if (!options.AutoLoadModel)
        {
            throw new InvalidOperationException(
                $"Model '{options.Model}' is not visible to LM Studio. Load it in LM Studio, enable Just-in-Time loading, or set LATTICE_LMSTUDIO_AUTO_LOAD=true.");
        }

        return await LoadModelAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> LoadModelAsync(CancellationToken cancellationToken)
    {
        using var models = await GetJsonAsync("api/v1/models", cancellationToken)
            .ConfigureAwait(false);
        var availableModel = models.RootElement
            .GetProperty("models")
            .EnumerateArray()
            .FirstOrDefault(model =>
                string.Equals(
                    GetString(model, "key"),
                    options.Model,
                    StringComparison.Ordinal));

        if (availableModel.ValueKind == JsonValueKind.Undefined)
        {
            throw new InvalidOperationException(
                $"Model '{options.Model}' is not installed in LM Studio. Download or import it first.");
        }

        if (!string.Equals(GetString(availableModel, "type"), "llm", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Model '{options.Model}' is not an LM Studio language model.");
        }

        if (availableModel.TryGetProperty("loaded_instances", out var instances) &&
            instances.ValueKind == JsonValueKind.Array &&
            instances.GetArrayLength() > 0)
        {
            return GetString(instances[0], "id") ?? options.Model;
        }

        using var request = CreateRequest(
            HttpMethod.Post,
            "api/v1/models/load",
            new { model = options.Model });
        using var response = await httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        var responseBody = await ReadSuccessfulResponseAsync(response, cancellationToken)
            .ConfigureAwait(false);
        using var loaded = JsonDocument.Parse(responseBody);

        return GetString(loaded.RootElement, "instance_id") ?? options.Model;
    }

    private async Task StartServerAsync(CancellationToken cancellationToken)
    {
        if (!options.BaseAddress.IsLoopback ||
            !string.Equals(options.BaseAddress.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Automatic server startup is restricted to an HTTP loopback address.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = options.CliPath,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("server");
        startInfo.ArgumentList.Add("start");
        startInfo.ArgumentList.Add("--port");
        startInfo.ArgumentList.Add(options.BaseAddress.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));

        using var process = Process.Start(startInfo);

        if (process is null)
        {
            throw new InvalidOperationException(
                $"Could not start LM Studio CLI '{options.CliPath}'. Ensure the lms command is installed and available on PATH.");
        }

        var startedAt = Stopwatch.GetTimestamp();

        while (Stopwatch.GetElapsedTime(startedAt) < TimeSpan.FromSeconds(30))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var models = await GetJsonAsync("v1/models", cancellationToken)
                    .ConfigureAwait(false);
                return;
            }
            catch (HttpRequestException exception) when (exception.StatusCode is null)
            {
                if (process.HasExited && process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"LM Studio CLI exited with code {process.ExitCode} while starting the server.");
                }

                await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        throw new TimeoutException(
            "LM Studio did not start its local server within 30 seconds.");
    }

    private async Task<JsonDocument> GetJsonAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, relativePath);
        using var response = await httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        var body = await ReadSuccessfulResponseAsync(response, cancellationToken)
            .ConfigureAwait(false);

        return JsonDocument.Parse(body);
    }

    private HttpRequestMessage CreateRequest(
        HttpMethod method,
        string relativePath,
        object? body = null)
    {
        var request = new HttpRequestMessage(
            method,
            new Uri(options.BaseAddress, relativePath));

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (options.ApiToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                options.ApiToken);
        }

        return request;
    }

    private static async Task<string> ReadSuccessfulResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var detail = body.Length > 500 ? body[..500] : body;
            throw new HttpRequestException(
                $"LM Studio returned HTTP {(int)response.StatusCode}: {detail}",
                null,
                response.StatusCode);
        }

        return body;
    }

    private SemanticTranslationResponse ParseTranslation(string content)
    {
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("candidates", out var candidatesElement) ||
            candidatesElement.ValueKind != JsonValueKind.Array ||
            candidatesElement.GetArrayLength() > MaximumCandidates)
        {
            throw new JsonException(
                "The translator response must contain a candidates array with no more than four entries.");
        }

        var message = root.TryGetProperty("message", out var messageElement)
            ? messageElement.ValueKind switch
            {
                JsonValueKind.String => messageElement.GetString(),
                JsonValueKind.Null => null,
                _ => throw new JsonException("The translator message must be a string or null.")
            }
            : throw new JsonException("The translator response is missing its message property.");
        var candidates = ImmutableArray.CreateBuilder<SemanticTranslationCandidate>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var candidateElement in candidatesElement.EnumerateArray())
        {
            if (candidateElement.ValueKind != JsonValueKind.String)
            {
                throw new JsonException("Every translation candidate must be an LCE string.");
            }

            var lce = candidateElement.GetString();

            if (string.IsNullOrWhiteSpace(lce))
            {
                throw new JsonException("Translation candidates cannot be empty.");
            }

            var parsed = parser.Parse(lce);

            if (!parsed.IsSuccess || parsed.Utterance is null)
            {
                return new SemanticTranslationResponse(
                    ImmutableArray<SemanticTranslationCandidate>.Empty,
                    $"The model output was outside LCE 0.9: {parsed.Error}");
            }

            var key = SemanticFormatter.Format(parsed.Utterance);

            if (seen.Add(key))
            {
                candidates.Add(new SemanticTranslationCandidate(
                    parsed.Utterance.Kind,
                    parsed.Utterance.Meaning));
            }
        }

        return new SemanticTranslationResponse(candidates.ToImmutable(), message);
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
