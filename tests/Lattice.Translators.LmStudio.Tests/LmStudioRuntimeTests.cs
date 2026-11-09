using System.Net;
using System.Text.Json;
using Lattice.Translators.LmStudio;
using Xunit;

namespace Lattice.Translators.LmStudio.Tests;

public sealed class LmStudioRuntimeTests
{
    private static readonly Uri ApiAddress = new("http://127.0.0.1:1234/api/v1/chat");

    [Fact]
    public async Task Starts_the_server_and_lists_only_installed_language_models()
    {
        var serverStarted = false;
        var processRunner = new FakeLmStudioProcessRunner
        {
            OnStart = () => serverStarted = true
        };
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath == "/v1/models")
            {
                return Task.FromResult(serverStarted
                    ? JsonResponse("""{"data":[]}""")
                    : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }

            if (request.RequestUri.AbsolutePath == "/api/v1/models")
            {
                return Task.FromResult(JsonResponse(
                    """{"models":[{"key":"publisher/gemma-270m-it","display_name":"Gemma 3 270M IT","type":"llm","params_string":"270M","size_bytes":300000000,"loaded_instances":[]},{"key":"publisher/embed","display_name":"Embedding","type":"embedding"}]}"""));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });
        var runtime = CreateRuntime(handler, processRunner);

        var models = await runtime.ListInstalledModelsAsync();

        var model = Assert.Single(models);
        Assert.Equal("publisher/gemma-270m-it", model.Key);
        Assert.Equal("Gemma 3 270M IT", model.DisplayName);
        Assert.Equal("270M", model.Parameters);
        var started = Assert.Single(processRunner.DetachedStarts);
        Assert.Equal("lms", started.Executable);
        Assert.Equal(
            new[] { "server", "start", "--port", "1234", "--bind", "127.0.0.1" },
            started.Arguments);
    }

    [Fact]
    public async Task Loads_a_selected_model_with_a_stable_identifier_and_waits_until_ready()
    {
        var loaded = false;
        var processRunner = new FakeLmStudioProcessRunner
        {
            RunResult = (_, _) =>
            {
                loaded = true;
                return new LmStudioProcessResult(0, "Loaded", string.Empty);
            }
        };
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath == "/v1/models")
            {
                return Task.FromResult(JsonResponse(
                    loaded
                        ? """{"data":[{"id":"publisher/gemma-270m-it"}]}"""
                        : """{"data":[]}"""));
            }

            if (request.RequestUri.AbsolutePath == "/api/v1/models")
            {
                return Task.FromResult(JsonResponse(
                    """{"models":[{"key":"publisher/gemma-270m-it","display_name":"Gemma 3 270M IT","type":"llm","loaded_instances":[]}]}"""));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });
        var runtime = CreateRuntime(handler, processRunner);

        var identifier = await runtime.EnsureModelReadyAsync("publisher/gemma-270m-it");

        Assert.Equal("publisher/gemma-270m-it", identifier);
        var command = Assert.Single(processRunner.Runs);
        Assert.Equal("lms", command.Executable);
        Assert.Equal(
            new[] { "load", "publisher/gemma-270m-it", "--identifier", "publisher/gemma-270m-it" },
            command.Arguments);
    }

    [Fact]
    public async Task Does_not_report_server_ready_when_model_catalog_is_unavailable()
    {
        var processRunner = new FakeLmStudioProcessRunner();
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        var runtime = CreateRuntime(handler, processRunner);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await runtime.ListInstalledModelsAsync());

        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
        Assert.Empty(processRunner.DetachedStarts);
    }

    [Fact]
    public async Task Reports_cli_load_failures_with_the_process_error()
    {
        var processRunner = new FakeLmStudioProcessRunner
        {
            RunResult = (_, _) => new LmStudioProcessResult(1, string.Empty, "Insufficient memory.")
        };
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath == "/v1/models")
            {
                return Task.FromResult(JsonResponse("""{"data":[]}"""));
            }

            if (request.RequestUri.AbsolutePath == "/api/v1/models")
            {
                return Task.FromResult(JsonResponse(
                    """{"models":[{"key":"publisher/gemma-270m-it","display_name":"Gemma 3 270M IT","type":"llm","loaded_instances":[]}]}"""));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });
        var runtime = CreateRuntime(handler, processRunner);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await runtime.EnsureModelReadyAsync("publisher/gemma-270m-it"));

        Assert.Contains("Insufficient memory.", exception.Message);
    }

    private static LmStudioRuntime CreateRuntime(
        HttpMessageHandler handler,
        ILmStudioProcessRunner processRunner)
    {
        var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        return new LmStudioRuntime(client, new LmStudioConfiguration
        {
            ApiUrl = ApiAddress.AbsoluteUri,
            ServerStartupTimeoutSeconds = 1,
            ModelLoadTimeoutSeconds = 1
        }, processRunner);
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
}
