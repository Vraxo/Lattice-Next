using System.Text.Json;
using Lattice.Core.Tools;
using Lattice.Tooling.Stdio;
using Xunit;

namespace Lattice.Core.Tests.Tools;

public sealed class StdioToolProviderTests
{
    [Fact]
    public void Discovers_external_tool_descriptors_without_starting_processes()
    {
        var root = CreateTemporaryDirectory();

        try
        {
            var toolDirectory = Directory.CreateDirectory(Path.Combine(root, "sample-tool")).FullName;
            var markerPath = Path.Combine(toolDirectory, "started.txt");

            WriteManifest(
                toolDirectory,
                CreateManifest(
                    "sample-tool",
                    "/bin/sh",
                    ["-c", $"touch '{markerPath}'"],
                    inputs: []));

            var discovered = StdioToolProvider.TryDiscover(root, out var tools, out var error);

            Assert.True(discovered, error);
            var tool = Assert.Single(tools);
            Assert.Equal("sample-tool", tool.Descriptor.Id);
            Assert.Equal("Sample tool", tool.Descriptor.Name);
            Assert.False(File.Exists(markerPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Parses_input_descriptors_and_permissions_from_manifest()
    {
        var root = CreateTemporaryDirectory();

        try
        {
            var toolDirectory = Directory.CreateDirectory(Path.Combine(root, "reader")).FullName;

            WriteManifest(
                toolDirectory,
                CreateManifest(
                    "reader",
                    "python",
                    [],
                    [
                        new { name = "path", description = "Path to inspect.", type = "string", required = true },
                        new { name = "depth", description = "Maximum depth.", type = "number", required = false }
                    ],
                    ["read"]));

            var discovered = StdioToolProvider.TryDiscover(root, out var tools, out var error);

            Assert.True(discovered, error);
            var descriptor = Assert.Single(tools).Descriptor;

            Assert.Equal(ToolPermission.Read, descriptor.RequiredPermissions);
            Assert.Equal(2, descriptor.Inputs.Length);
            Assert.Equal(ToolInputType.String, descriptor.Inputs[0].Type);
            Assert.True(descriptor.Inputs[0].Required);
            Assert.Equal(ToolInputType.Number, descriptor.Inputs[1].Type);
            Assert.False(descriptor.Inputs[1].Required);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Parses_manifest_intent_patterns_and_argument_bindings()
    {
        var root = CreateTemporaryDirectory();

        try
        {
            var toolDirectory = Directory.CreateDirectory(Path.Combine(root, "echo")).FullName;
            var manifest = JsonSerializer.Serialize(new
            {
                protocolVersion = 1,
                id = "echo",
                name = "Echo",
                description = "Returns one supplied word.",
                command = "python",
                arguments = Array.Empty<string>(),
                inputs = new[]
                {
                    new { name = "text", description = "Text to echo.", type = "string", required = true },
                    new { name = "count", description = "Number of repeats.", type = "number", required = true }
                },
                permissions = Array.Empty<string>(),
                intents = new[]
                {
                    new
                    {
                        pattern = "repeat {text:word} {count:number} times",
                        arguments = new Dictionary<string, string>
                        {
                            ["text"] = "$text",
                            ["count"] = "$count"
                        },
                        response = "Repeated {text} {result} times."
                    }
                }
            });
            WriteManifest(toolDirectory, manifest);

            var discovered = StdioToolProvider.TryDiscover(root, out var tools, out var error);

            Assert.True(discovered, error);
            var descriptor = Assert.Single(tools).Descriptor;
            var intent = Assert.Single(descriptor.Intents);

            Assert.Equal("repeat {text:word} {count:number} times", intent.Pattern);
            Assert.Equal("text", intent.Arguments["text"].Reference);
            Assert.Equal("count", intent.Arguments["count"].Reference);
            Assert.Equal("Repeated {text} {result} times.", intent.ResponseTemplate);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(
        """{"pattern":"do {value:unknown}","arguments":{"value":"$value"},"response":"Result {result}."}""")]
    [InlineData(
        """{"pattern":"do {value:number}","arguments":{"value":"$missing"},"response":"Result {result}."}""")]
    [InlineData(
        """{"pattern":"do {value:number}","arguments":{"value":"$value"},"response":"Result {unknown}."}""")]
    [InlineData(
        """{"pattern":"do {value:number}","arguments":{},"response":"Result {result}."}""")]
    public void Rejects_invalid_manifest_intent_declarations(string intent)
    {
        var root = CreateTemporaryDirectory();

        try
        {
            var toolDirectory = Directory.CreateDirectory(Path.Combine(root, "invalid-intent")).FullName;
            var manifest = $"""
                {
                  "protocolVersion": 1,
                  "id": "invalid-intent",
                  "name": "Invalid intent",
                  "description": "Contains an invalid intent declaration.",
                  "command": "python",
                  "inputs": [
                    {
                      "name": "value",
                      "description": "Numeric input.",
                      "type": "number",
                      "required": true
                    }
                  ],
                  "intents": [{{intent}}]
                }
                """;
            WriteManifest(toolDirectory, manifest);

            var discovered = StdioToolProvider.TryDiscover(root, out var tools, out var error);

            Assert.False(discovered);
            Assert.Empty(tools);
            Assert.Contains("invalid", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Rejects_unsupported_protocol_versions()
    {
        var root = CreateTemporaryDirectory();

        try
        {
            var toolDirectory = Directory.CreateDirectory(Path.Combine(root, "future-tool")).FullName;
            var manifest = JsonSerializer.Serialize(new
            {
                protocolVersion = 2,
                id = "future-tool",
                name = "Future tool",
                description = "Uses an unknown protocol.",
                command = "python"
            });
            WriteManifest(toolDirectory, manifest);

            var discovered = StdioToolProvider.TryDiscover(root, out var tools, out var error);

            Assert.False(discovered);
            Assert.Empty(tools);
            Assert.Contains("unsupported protocol version", error);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Rejects_unknown_manifest_properties()
    {
        var root = CreateTemporaryDirectory();

        try
        {
            var toolDirectory = Directory.CreateDirectory(Path.Combine(root, "typo-tool")).FullName;
            var manifest = JsonSerializer.Serialize(new
            {
                protocolVersion = 1,
                id = "typo-tool",
                name = "Typo tool",
                description = "Has an unknown field.",
                command = "python",
                commmand = "python"
            });
            WriteManifest(toolDirectory, manifest);

            var discovered = StdioToolProvider.TryDiscover(root, out var tools, out var error);

            Assert.False(discovered);
            Assert.Empty(tools);
            Assert.Contains("unknown properties", error);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Rejects_relative_executables_outside_the_tool_directory()
    {
        var root = CreateTemporaryDirectory();

        try
        {
            var toolDirectory = Directory.CreateDirectory(Path.Combine(root, "unsafe-tool")).FullName;

            WriteManifest(
                toolDirectory,
                CreateManifest("unsafe-tool", "../outside", [], inputs: []));

            var discovered = StdioToolProvider.TryDiscover(root, out var tools, out var error);

            Assert.False(discovered);
            Assert.Empty(tools);
            Assert.Contains("outside its tool directory", error);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Invokes_external_process_using_versioned_json_lines()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var root = CreateTemporaryDirectory();

        try
        {
            var toolDirectory = Directory.CreateDirectory(Path.Combine(root, "echo")).FullName;
            var script = """
                IFS= read -r request
                request_id=$(printf '%s' "$request" | cut -d '"' -f 6)
                case "$request" in *'"value":"hello"'*) value=hello ;; *) value=missing ;; esac
                printf '{"protocolVersion":1,"requestId":"%s","success":true,"result":{"received":"%s"}}\n' "$request_id" "$value"
                """;

            WriteManifest(
                toolDirectory,
                CreateManifest(
                    "echo",
                    "/bin/sh",
                    ["-c", script],
                    [new { name = "value", description = "Value to echo.", type = "string", required = true }]));

            Assert.True(
                StdioToolProvider.TryDiscover(root, out var tools, out var discoveryError),
                discoveryError);

            var registry = new ToolRegistry(
                tools,
                executionTimeout: TimeSpan.FromSeconds(5));

            var result = await registry.TryInvokeAsync(
                "echo",
                """{"value":"hello"}""",
                TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess, result.Error);
            Assert.Equal("echo", result.Observation!.ToolId);
            Assert.Equal("""{"received":"hello"}""", result.Observation.Content);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lattice-stdio-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateManifest(
        string id,
        string command,
        IReadOnlyList<string> arguments,
        object[]? inputs = null,
        string[]? permissions = null) =>
        JsonSerializer.Serialize(new
        {
            protocolVersion = 1,
            id,
            name = "Sample tool",
            description = "A test external capability.",
            command,
            arguments,
            inputs = inputs ?? [],
            permissions = permissions ?? []
        });

    private static void WriteManifest(string toolDirectory, string manifest) =>
        File.WriteAllText(Path.Combine(toolDirectory, "tool.json"), manifest);
}
