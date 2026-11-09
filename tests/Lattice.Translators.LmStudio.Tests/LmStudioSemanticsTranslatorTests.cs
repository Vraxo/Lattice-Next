using System.Net;
using System.Text.Json;
using Lattice.Core.Intelligence;
using Lattice.Core.Semantics;
using Lattice.Translators.LmStudio;
using Xunit;

namespace Lattice.Translators.LmStudio.Tests;

public sealed class LmStudioSemanticsTranslatorTests
{
    private const string Model = "google/gemma-3-270m-it";
    private const string ApiUrl = "http://127.0.0.1:1234/v1/chat/completions";

    [Fact]
    public async Task Converts_a_valid_lce_response_into_a_semantic_candidate()
    {
        var handler = CreateHandler(
            """{"candidates":["Fido is a dog."],"message":null}""");
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var configuration = CreateConfiguration();
        var translator = CreateTranslator(client, configuration);

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
        var responseFormat = requestBody.RootElement.GetProperty("response_format");
        Assert.Equal("json_schema", responseFormat.GetProperty("type").GetString());

        var jsonSchema = responseFormat.GetProperty("json_schema");
        Assert.Equal("lattice_translation", jsonSchema.GetProperty("name").GetString());
        Assert.True(jsonSchema.GetProperty("strict").GetBoolean());

        var schema = jsonSchema.GetProperty("schema");
        Assert.Equal("object", schema.GetProperty("type").GetString());
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        Assert.Contains(
            schema.GetProperty("required").EnumerateArray(),
            property => property.GetString() == "candidates");
        Assert.Contains(
            schema.GetProperty("required").EnumerateArray(),
            property => property.GetString() == "message");
        Assert.Equal(
            "array",
            schema.GetProperty("properties").GetProperty("candidates").GetProperty("type").GetString());
        Assert.Equal(
            "string",
            schema.GetProperty("properties").GetProperty("candidates")
                .GetProperty("items").GetProperty("type").GetString());
        Assert.Equal(
            "string",
            schema.GetProperty("properties").GetProperty("message")
                .GetProperty("type")[0].GetString());
        Assert.Equal(
            "null",
            schema.GetProperty("properties").GetProperty("message")
                .GetProperty("type")[1].GetString());
    }

    [Fact]
    public async Task Returns_no_candidates_when_the_model_abstains()
    {
        var handler = CreateHandler(
            """{"candidates":[],"message":"Negation is not supported."}""");
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var translator = CreateTranslator(client, CreateConfiguration());

        var response = await translator.TranslateAsync("No dog is a cat.");

        Assert.Empty(response.Candidates);
        Assert.Equal("Negation is not supported.", response.Message);
    }

    [Fact]
    public async Task Rejects_the_entire_model_response_when_any_candidate_is_not_lce()
    {
        var handler = CreateHandler(
            """{"candidates":["Fido is a dog.","Fido is friendly and playful."],"message":null}""");
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var translator = CreateTranslator(client, CreateConfiguration());

        var response = await translator.TranslateAsync("Fido is a dog and is friendly.");

        Assert.Empty(response.Candidates);
        Assert.Contains("outside LCE 0.9", response.Message);
    }

    [Fact]
    public async Task Preserves_multiple_distinct_valid_candidates()
    {
        var handler = CreateHandler(
            """{"candidates":["Fido is a dog.","Fido is a pet."],"message":null}""");
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var translator = CreateTranslator(client, CreateConfiguration());

        var response = await translator.TranslateAsync("Fido is a pet dog.");

        Assert.Equal(2, response.Candidates.Length);
    }

    [Fact]
    public async Task Uses_the_loaded_instance_identifier_for_chat_completions()
    {
        const string instanceIdentifier = "gemma-session-1";
        var handler = CreateHandler(
            """{"candidates":["Fido is a dog."],"message":null}""",
            loadedModelIdentifier: instanceIdentifier);
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var configuration = CreateConfiguration() with
        {
            ModelIdentifier = instanceIdentifier
        };
        var translator = CreateTranslator(client, configuration);

        await translator.TranslateAsync("Fido is a dog.");

        var chatRequest = Assert.Single(handler.Requests.Where(
            request => request.Uri!.AbsolutePath == "/v1/chat/completions"));
        using var body = JsonDocument.Parse(chatRequest.Body!);
        Assert.Equal(instanceIdentifier, body.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public async Task Reports_when_a_selected_model_is_no_longer_installed()
    {
        var handler = CreateHandler(
            """{"candidates":["Fido is a dog."],"message":null}""",
            includeModel: false);
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var translator = CreateTranslator(client, CreateConfiguration());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await translator.TranslateAsync("Fido is a dog."));

        Assert.Contains("not installed", exception.Message);
    }

    private static LmStudioConfiguration CreateConfiguration() =>
        new()
        {
            ApiUrl = ApiUrl,
            ModelKey = Model,
            ModelIdentifier = Model
        };

    private static LmStudioSemanticsTranslator CreateTranslator(
        HttpClient client,
        LmStudioConfiguration configuration)
    {
        var runtime = new LmStudioRuntime(
            client,
            configuration,
            new FakeLmStudioProcessRunner());
        return new LmStudioSemanticsTranslator(client, configuration, runtime);
    }

    private static StubHttpMessageHandler CreateHandler(
        string translationJson,
        bool includeModel = true,
        string loadedModelIdentifier = Model)
    {
        var modelJson = includeModel
            ? JsonSerializer.Serialize(new
            {
                data = new[]
                {
                    new
                    {
                        id = loadedModelIdentifier
                    }
                }
            })
            : """{"data":[]}""";
        var installedModelJson = includeModel
            ? """{"models":[{"key":"google/gemma-3-270m-it","display_name":"Gemma 3 270M IT","type":"llm","loaded_instances":[]}]}"""
            : """{"models":[]}""";

        return new StubHttpMessageHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Get &&
                request.RequestUri!.AbsolutePath == "/v1/models")
            {
                return Task.FromResult(JsonResponse(modelJson));
            }

            if (request.Method == HttpMethod.Get &&
                request.RequestUri!.AbsolutePath == "/api/v1/models")
            {
                return Task.FromResult(JsonResponse(installedModelJson));
            }

            if (request.Method == HttpMethod.Post &&
                request.RequestUri!.AbsolutePath == "/v1/chat/completions")
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
