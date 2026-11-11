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
    private const string ApiUrl = "http://127.0.0.1:1234/api/v1/chat";

    [Fact]
    public async Task Converts_a_valid_lce_response_into_a_semantic_candidate()
    {
        var handler = CreateHandler(
            """{"status":"translated","candidates":["Fido is a dog."],"message":null}""");
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var configuration = CreateConfiguration();
        var translator = CreateTranslator(client, configuration);

        var response = await translator.TranslateAsync("Fido is a canine.");

        var candidate = Assert.Single(response.Candidates);
        Assert.Equal(UtteranceKind.Assertion, candidate.Kind);
        Assert.Equal("Fido is a dog.", candidate.LceText);
        Assert.Equal(
            "dog(fido)",
            SemanticFormatter.Format(new SemanticUtterance(candidate.Kind, candidate.Meaning, "Fido is a canine.")));

        var modelRequest = Assert.Single(handler.Requests.Where(
            request => request.Method == HttpMethod.Post));
        Assert.Equal("/api/v1/chat", modelRequest.Uri!.AbsolutePath);
        using var requestBody = JsonDocument.Parse(modelRequest.Body!);
        var root = requestBody.RootElement;
        Assert.Equal(Model, root.GetProperty("model").GetString());
        Assert.Equal("Fido is a canine.", root.GetProperty("input").GetString());
        Assert.Equal("off", root.GetProperty("reasoning").GetString());
        Assert.False(root.GetProperty("store").GetBoolean());
        Assert.Equal(320, root.GetProperty("max_output_tokens").GetInt32());

        var systemPrompt = root.GetProperty("system_prompt").GetString();
        Assert.Contains("Return only a JSON object", systemPrompt);
        Assert.Contains("Never put explanations, refusals, or ordinary-language text in \"candidates\".", systemPrompt);
        Assert.Contains("Each request contains one utterance, not a multi-sentence turn.", systemPrompt);
        Assert.Contains("Input: Could Fido be a dog?", systemPrompt);
        Assert.Contains("{\"status\":\"unsupported\",\"candidates\":[],\"message\":\"LCE 0.9 cannot express the possibility in 'could'.\"}", systemPrompt);
    }

    [Fact]
    public async Task Ignores_reasoning_output_and_parses_only_the_assistant_message()
    {
        var handler = CreateHandler(
            """{"status":"translated","candidates":["Fido is a dog."],"message":null}""",
            includeReasoningOutput: true);
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var translator = CreateTranslator(client, CreateConfiguration());

        var response = await translator.TranslateAsync("Fido is a dog.");

        Assert.Single(response.Candidates);
    }

    [Fact]
    public async Task Uses_explicit_unsupported_outcome_for_a_multi_utterance_request()
    {
        var handler = CreateHandler(
            """{"status":"unsupported","candidates":[],"message":"Only one utterance can be translated per request."}""");
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var translator = CreateTranslator(client, CreateConfiguration());

        var response = await translator.TranslateAsync("Fido is a dog. Rex is a cat.");

        Assert.Equal(SemanticTranslationOutcome.Unsupported, response.Outcome);
        Assert.Empty(response.Candidates);
        var request = Assert.Single(handler.Requests.Where(x => x.Method == HttpMethod.Post));
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal("Fido is a dog. Rex is a cat.", body.RootElement.GetProperty("input").GetString());
    }

    [Fact]
    public async Task Reports_reasoning_only_output_when_no_assistant_message_is_returned()
    {
        var handler = CreateHandler(
            """{"status":"translated","candidates":["Fido is a dog."],"message":null}""",
            reasoningOnly: true);
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var translator = CreateTranslator(client, CreateConfiguration());

        var exception = await Assert.ThrowsAsync<JsonException>(async () =>
            await translator.TranslateAsync("Fido is a dog."));

        Assert.Contains("no assistant message", exception.Message);
        Assert.Contains("reasoning", exception.Message);
        Assert.Contains("36 reasoning token(s)", exception.Message);
    }

    [Fact]
    public async Task Returns_no_candidates_when_the_model_abstains()
    {
        var handler = CreateHandler(
            """{"status":"unsupported","candidates":[],"message":"Negation is not supported."}""");
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var translator = CreateTranslator(client, CreateConfiguration());

        var response = await translator.TranslateAsync("No dog is a cat.");

        Assert.Empty(response.Candidates);
        Assert.Equal(SemanticTranslationOutcome.Unsupported, response.Outcome);
        Assert.Equal("Negation is not supported.", response.Message);
    }

    [Fact]
    public async Task Rejects_the_entire_model_response_when_any_candidate_is_not_lce()
    {
        var handler = CreateHandler(
            """{"status":"ambiguous","candidates":["Fido is a dog.","Fido is friendly and playful."],"message":null}""");
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var translator = CreateTranslator(client, CreateConfiguration());

        var response = await translator.TranslateAsync("Fido is a dog and is friendly.");

        Assert.Empty(response.Candidates);
        Assert.Equal(SemanticTranslationOutcome.Invalid, response.Outcome);
        Assert.Contains("not valid LCE 0.9", response.Message);
        Assert.Contains(JsonSerializer.Serialize("Fido is friendly and playful."), response.Message);
    }

    [Fact]
    public async Task Preserves_multiple_distinct_valid_candidates()
    {
        var handler = CreateHandler(
            """{"status":"ambiguous","candidates":["Fido is a dog.","Fido is a pet."],"message":null}""");
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var translator = CreateTranslator(client, CreateConfiguration());

        var response = await translator.TranslateAsync("Fido is a pet dog.");

        Assert.Equal(2, response.Candidates.Length);
    }

    [Fact]
    public async Task Uses_the_loaded_instance_identifier_for_native_chat()
    {
        const string instanceIdentifier = "gemma-session-1";
        var handler = CreateHandler(
            """{"status":"translated","candidates":["Fido is a dog."],"message":null}""",
            loadedModelIdentifier: instanceIdentifier);
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var configuration = CreateConfiguration() with
        {
            ModelIdentifier = instanceIdentifier
        };
        var translator = CreateTranslator(client, configuration);

        await translator.TranslateAsync("Fido is a dog.");

        var chatRequest = Assert.Single(handler.Requests.Where(
            request => request.Uri!.AbsolutePath == "/api/v1/chat"));
        using var body = JsonDocument.Parse(chatRequest.Body!);
        Assert.Equal(instanceIdentifier, body.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public async Task Reports_when_a_selected_model_is_no_longer_installed()
    {
        var handler = CreateHandler(
            """{"status":"translated","candidates":["Fido is a dog."],"message":null}""",
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
        string loadedModelIdentifier = Model,
        bool includeReasoningOutput = false,
        bool reasoningOnly = false)
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
                request.RequestUri!.AbsolutePath == "/api/v1/chat")
            {
                return Task.FromResult(NativeChatResponse(
                    translationJson,
                    includeReasoningOutput,
                    reasoningOnly));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });
    }

    private static HttpResponseMessage NativeChatResponse(
        string translationJson,
        bool includeReasoningOutput,
        bool reasoningOnly)
    {
        object[] output = reasoningOnly
            ? new object[]
            {
                new { type = "reasoning", content = "Internal reasoning is not an answer." }
            }
            : includeReasoningOutput
                ? new object[]
                {
                    new { type = "reasoning", content = "Internal reasoning must not be parsed." },
                    new { type = "message", content = translationJson }
                }
                : new object[]
                {
                    new { type = "message", content = translationJson }
                };

        var response = JsonSerializer.Serialize(new
        {
            model_instance_id = Model,
            output,
            stats = new
            {
                reasoning_output_tokens = reasoningOnly ? 36 : includeReasoningOutput ? 12 : 0
            }
        });

        return JsonResponse(response);
    }

    private static HttpResponseMessage JsonResponse(string content) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
        };
}
