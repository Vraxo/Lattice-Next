using System.Net;
using System.Text.Json;
using Lattice.Core.Intelligence;
using Lattice.Core.Semantics;
using Lattice.Translators.LmStudio;
using Xunit;

namespace Lattice.Translators.LmStudio.Tests;

public sealed class LmStudioSemanticsTranslatorTests
{
    private static readonly Uri BaseAddress = new("http://127.0.0.1:1234/");
    private const string Model = "google/gemma-3-270m-it";

    [Fact]
    public async Task Converts_a_valid_lce_response_into_a_semantic_candidate()
    {
        var handler = CreateHandler(
            """{"candidates":["Fido is a dog."],"message":null}""");
        using var client = new HttpClient(handler);
        var translator = new LmStudioSemanticsTranslator(
            client,
            new LmStudioOptions(BaseAddress, Model));

        var response = await translator.TranslateAsync("Fido is a canine.");

        var candidate = Assert.Single(response.Candidates);
        Assert.Equal(UtteranceKind.Assertion, candidate.Kind);
        Assert.Equal(
            "dog(fido)",
            SemanticFormatter.Format(new SemanticUtterance(candidate.Kind, candidate.Meaning, "Fido is a canine.")));

        var modelRequest = Assert.Single(handler.Requests.Where(
            request => request.Method == HttpMethod.Post));
        Assert.Equal("/v1/chat/completions", modelRequest.Uri!.AbsolutePath);
        using var requestBody = JsonDocument.Parse(modelRequest.Body!);
        Assert.Equal(Model, requestBody.RootElement.GetProperty("model").GetString());
        Assert.Equal("json_object", requestBody.RootElement
            .GetProperty("response_format")
            .GetProperty("type")
            .GetString());
    }

    [Fact]
    public async Task Returns_no_candidates_when_the_model_abstains()
    {
        var handler = CreateHandler(
            """{"candidates":[],"message":"Negation is not supported."}""");
        using var client = new HttpClient(handler);
        var translator = new LmStudioSemanticsTranslator(
            client,
            new LmStudioOptions(BaseAddress, Model));

        var response = await translator.TranslateAsync("No dog is a cat.");

        Assert.Empty(response.Candidates);
        Assert.Equal("Negation is not supported.", response.Message);
    }

    [Fact]
    public async Task Rejects_the_entire_model_response_when_any_candidate_is_not_lce()
    {
        var handler = CreateHandler(
            """{"candidates":["Fido is a dog.","Fido is friendly and playful."],"message":null}""");
        using var client = new HttpClient(handler);
        var translator = new LmStudioSemanticsTranslator(
            client,
            new LmStudioOptions(BaseAddress, Model));

        var response = await translator.TranslateAsync("Fido is a dog and is friendly.");

        Assert.Empty(response.Candidates);
        Assert.Contains("outside LCE 0.9", response.Message);
    }

    [Fact]
    public async Task Preserves_multiple_distinct_valid_candidates()
    {
        var handler = CreateHandler(
            """{"candidates":["Fido is a dog.","Fido is a pet."],"message":null}""");
        using var client = new HttpClient(handler);
        var translator = new LmStudioSemanticsTranslator(
            client,
            new LmStudioOptions(BaseAddress, Model));

        var response = await translator.TranslateAsync("Fido is a pet dog.");

        Assert.Equal(2, response.Candidates.Length);
    }

    [Fact]
    public async Task Loads_an_installed_model_when_auto_load_is_enabled()
    {
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            var path = request.RequestUri!.AbsolutePath;

            if (request.Method == HttpMethod.Get && path == "/v1/models")
            {
                return Task.FromResult(JsonResponse("""{"data":[]}"""));
            }

            if (request.Method == HttpMethod.Get && path == "/api/v1/models")
            {
                return Task.FromResult(JsonResponse(
                    """{"models":[{"key":"google/gemma-3-270m-it","type":"llm","loaded_instances":[]}]}"""));
            }

            if (request.Method == HttpMethod.Post && path == "/api/v1/models/load")
            {
                return Task.FromResult(JsonResponse(
                    """{"instance_id":"gemma-session-1","status":"loaded"}"""));
            }

            if (request.Method == HttpMethod.Post && path == "/v1/chat/completions")
            {
                var body = JsonDocument.Parse("""{"candidates":["Fido is a dog."],"message":null}""");
                var content = body.RootElement.GetRawText();
                body.Dispose();
                return Task.FromResult(CompletionResponse(content));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });

        using var client = new HttpClient(handler);
        var translator = new LmStudioSemanticsTranslator(
            client,
            new LmStudioOptions(BaseAddress, Model, autoLoadModel: true));

        var response = await translator.TranslateAsync("Fido is a dog.");

        Assert.Single(response.Candidates);
        var loadRequest = Assert.Single(handler.Requests.Where(
            request => request.Uri!.AbsolutePath == "/api/v1/models/load"));
        Assert.Contains(Model, loadRequest.Body);
        var chatRequest = Assert.Single(handler.Requests.Where(
            request => request.Uri!.AbsolutePath == "/v1/chat/completions"));
        using var chatBody = JsonDocument.Parse(chatRequest.Body!);
        Assert.Equal("gemma-session-1", chatBody.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public async Task Returns_a_clear_error_when_the_configured_model_is_not_visible()
    {
        var handler = CreateHandler(
            """{"candidates":["Fido is a dog."],"message":null}""",
            includeModel: false);
        using var client = new HttpClient(handler);
        var translator = new LmStudioSemanticsTranslator(
            client,
            new LmStudioOptions(BaseAddress, Model));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await translator.TranslateAsync("Fido is a dog."));

        Assert.Contains(Model, exception.Message);
        Assert.Contains("not visible", exception.Message);
    }

    private static StubHttpMessageHandler CreateHandler(
        string translationJson,
        bool includeModel = true)
    {
        var modelJson = includeModel
            ? """{"data":[{"id":"google/gemma-3-270m-it"}]}"""
            : """{"data":[]}""";

        return new StubHttpMessageHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath == "/v1/models")
            {
                return Task.FromResult(JsonResponse(modelJson));
            }

            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == "/v1/chat/completions")
            {
                return Task.FromResult(CompletionResponse(translationJson));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });
    }

    private static HttpResponseMessage CompletionResponse(string translationJson)
    {
        var content = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = translationJson
                    }
                }
            }
        });

        return JsonResponse(content);
    }

    private static HttpResponseMessage JsonResponse(string content) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
        };
}
