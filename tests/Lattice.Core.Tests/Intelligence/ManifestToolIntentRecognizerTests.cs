using System.Collections.Immutable;
using System.Text.Json;
using Lattice.Core.Intelligence;
using Lattice.Core.Tools;
using Lattice.Core.Tests.Tools;
using Lattice.Tooling.Stdio;
using Xunit;

namespace Lattice.Core.Tests.Intelligence;

public sealed class ManifestToolIntentRecognizerTests
{
    [Theory]
    [InlineData("What is 12 multiplied by 3?", 12, "multiply", 3)]
    [InlineData("12 plus 3", 12, "add", 3)]
    [InlineData("Calculate 12 divided by 3", 12, "divide", 3)]
    [InlineData("subtract 3 from 12", 12, "subtract", 3)]
    [InlineData("Multiply 12 by 3", 12, "multiply", 3)]
    public void Recognizes_calculator_patterns_declared_by_the_tool(
        string input,
        decimal expectedLeft,
        string expectedOperation,
        decimal expectedRight)
    {
        var resolution = CreateResolver().Resolve(input);

        Assert.True(resolution.IsResolved);
        Assert.Equal("calculator", resolution.Intent!.ToolId);

        using var arguments = JsonDocument.Parse(resolution.Intent.ArgumentsJson);
        Assert.Equal(expectedLeft, arguments.RootElement.GetProperty("left").GetDecimal());
        Assert.Equal(expectedOperation, arguments.RootElement.GetProperty("operation").GetString());
        Assert.Equal(expectedRight, arguments.RootElement.GetProperty("right").GetDecimal());
    }

    [Theory]
    [InlineData("Convert 150 cm to meters.", 150, "cm", "meters")]
    [InlineData("How many meters is 150 cm?", 150, "cm", "meters")]
    [InlineData("2 hours in minutes", 2, "hours", "minutes")]
    [InlineData("Please do convert 150 cm to meters.", 150, "cm", "meters")]
    public void Recognizes_unit_patterns_without_a_unit_specific_recognizer(
        string input,
        decimal expectedValue,
        string expectedFrom,
        string expectedTo)
    {
        var resolution = CreateResolver().Resolve(input);

        Assert.True(resolution.IsResolved);
        Assert.Equal("unit-converter", resolution.Intent!.ToolId);

        using var arguments = JsonDocument.Parse(resolution.Intent.ArgumentsJson);
        Assert.Equal(expectedValue, arguments.RootElement.GetProperty("value").GetDecimal());
        Assert.Equal(expectedFrom, arguments.RootElement.GetProperty("from").GetString());
        Assert.Equal(expectedTo, arguments.RootElement.GetProperty("to").GetString());
    }

    [Theory]
    [InlineData(
        "Search the web for the latest .NET 10 release notes.",
        "the latest .NET 10 release notes")]
    [InlineData(
        "Search the web for C# 13 and ASP.NET Core.",
        "C# 13 and ASP.NET Core")]
    public void Preserves_punctuation_in_manifest_text_captures(
        string input,
        string expectedQuery)
    {
        var definition = new ToolIntentDefinition(
            "[please] search the web for {query:text}",
            ImmutableDictionary<string, ToolIntentBinding>.Empty
                .Add("query", new ToolIntentBinding("query", null)),
            "Searching for {query}: {result}");
        var descriptor = new ToolDescriptor(
            "web-search",
            "Web search",
            "Searches the public web.",
            ImmutableArray.Create(
                new ToolInputDescriptor("query", "The search query.", ToolInputType.String)),
            Intents: ImmutableArray.Create(definition));
        var resolver = new ToolIntentResolver(
        [
            new ManifestToolIntentRecognizer(descriptor, definition)
        ]);

        var resolution = resolver.Resolve(input);

        Assert.True(resolution.IsResolved);
        Assert.Equal("web-search", resolution.Intent!.ToolId);

        using var arguments = JsonDocument.Parse(resolution.Intent.ArgumentsJson);
        Assert.Equal(
            expectedQuery,
            arguments.RootElement.GetProperty("query").GetString());
    }

    [Fact]
    public void Formats_responses_from_manifest_templates_and_tool_observations()
    {
        var conversion = CreateResolver().Resolve("Convert 150 cm to meters.");

        Assert.True(conversion.IsResolved);
        Assert.Equal(
            "150 cm is 1.5 meters.",
            conversion.Intent!.FormatResponse(new ToolObservation("unit-converter", "1.5")));

        var calculation = CreateResolver().Resolve("What is 12 multiplied by 3.");

        Assert.True(calculation.IsResolved);
        Assert.Equal(
            "12 multiplied by 3 is 36.",
            calculation.Intent!.FormatResponse(new ToolObservation("calculator", "36")));
    }

    [Fact]
    public void Resolves_a_follow_up_from_the_latest_numeric_observation()
    {
        var resolution = CreateResolver().Resolve(
            "Multiply that by 5.",
            new ToolObservation("calculator", "36"));

        Assert.True(resolution.IsResolved);
        Assert.Equal("calculator", resolution.Intent!.ToolId);

        using var arguments = JsonDocument.Parse(resolution.Intent.ArgumentsJson);
        Assert.Equal(36m, arguments.RootElement.GetProperty("left").GetDecimal());
        Assert.Equal("multiply", arguments.RootElement.GetProperty("operation").GetString());
        Assert.Equal(5m, arguments.RootElement.GetProperty("right").GetDecimal());
    }

    [Fact]
    public void Builds_a_sequential_chain_from_successive_numeric_observations()
    {
        var resolution = CreateResolver().Resolve(
            "Multiply that by 5, then subtract 10, then divide by 2.",
            new ToolObservation("calculator", "36"));

        Assert.True(resolution.IsResolved);
        var first = resolution.Intent!;
        AssertInvocation(first, 36m, "multiply", 5m);

        var second = Assert.IsType<ToolIntent>(
            first.Continue!(new ToolObservation("calculator", "180")));
        AssertInvocation(second, 180m, "subtract", 10m);

        Assert.Null(second.Continue!(new ToolObservation("calculator", "not a number")));

        var third = Assert.IsType<ToolIntent>(
            second.Continue!(new ToolObservation("calculator", "170")));
        AssertInvocation(third, 170m, "divide", 2m);
        Assert.Null(third.Continue);
        Assert.Equal(
            "170 divided by 2 is 85.",
            third.FormatResponse(new ToolObservation("calculator", "85")));
    }

    [Fact]
    public void Rejects_chains_longer_than_the_supported_limit()
    {
        var resolution = CreateResolver().Resolve(
            "Multiply that by 2, then add 1, then add 1, then add 1, then add 1, then add 1, then add 1, then add 1, then add 1.",
            new ToolObservation("calculator", "1"));

        Assert.Equal(ToolIntentResolutionStatus.NotRecognized, resolution.Status);
        Assert.Null(resolution.Intent);
    }

    [Fact]
    public void Does_not_continue_a_chain_from_a_non_numeric_observation()
    {
        var resolution = CreateResolver().Resolve(
            "Multiply that by 5, then subtract 10.",
            new ToolObservation("calculator", "not a number"));

        Assert.Equal(ToolIntentResolutionStatus.NotRecognized, resolution.Status);
        Assert.Null(resolution.Intent);
    }

    [Theory]
    [InlineData("Multiply that by 5, then maybe subtract 10.")]
    [InlineData("Multiply that by 5, then subtract 10 from 3.")]
    [InlineData("Multiply that by 5, then add by 10.")]
    [InlineData("Multiply that by 5, then divide 10 to that.")]
    public void Rejects_a_chain_when_any_step_is_unsupported(string input)
    {
        var recognized = CreateResolver().Resolve(
            input,
            new ToolObservation("calculator", "36"));

        Assert.Equal(ToolIntentResolutionStatus.NotRecognized, recognized.Status);
        Assert.Null(recognized.Intent);
    }

    [Fact]
    public void Rejects_a_follow_up_without_a_numeric_observation()
    {
        var resolution = CreateResolver().Resolve("Multiply that by 5.");

        Assert.Equal(ToolIntentResolutionStatus.NotRecognized, resolution.Status);
        Assert.Null(resolution.Intent);
    }

    [Fact]
    public void Bounds_search_effort_for_pathological_multiword_patterns()
    {
        const int slotCount = 12;
        var patternParts = new List<string>();
        var inputs = ImmutableArray.CreateBuilder<ToolInputDescriptor>();
        var bindings = ImmutableDictionary.CreateBuilder<string, ToolIntentBinding>();

        for (var index = 0; index < slotCount; index++)
        {
            var name = $"part{index}";
            patternParts.Add($"{{{name}:text}}");
            inputs.Add(new ToolInputDescriptor(name, "A text segment.", ToolInputType.String));
            bindings.Add(name, new ToolIntentBinding(name, null));

            if (index < slotCount - 1)
            {
                patternParts.Add("a");
            }
        }

        patternParts.Add("z");

        var definition = new ToolIntentDefinition(
            string.Join(" ", patternParts),
            bindings.ToImmutable(),
            "Matched {result}.");
        var descriptor = new ToolDescriptor(
            "pathological",
            "Pathological matcher test",
            "Exercises the matcher search budget.",
            inputs.ToImmutable(),
            Intents: ImmutableArray.Create(definition));
        var resolver = new ToolIntentResolver(
        [
            new ManifestToolIntentRecognizer(descriptor, definition)
        ]);
        var input = string.Join(" ", Enumerable.Repeat("a", 256));

        var resolution = resolver.Resolve(input);

        Assert.Equal(ToolIntentResolutionStatus.NotRecognized, resolution.Status);
        Assert.Null(resolution.Intent);
    }

    [Fact]
    public void Rejects_a_multiword_capture_when_more_than_one_boundary_matches()
    {
        var definition = new ToolIntentDefinition(
            "search {query:text} in {scope:text}",
            ImmutableDictionary<string, ToolIntentBinding>.Empty
                .Add("query", new ToolIntentBinding("query", null))
                .Add("scope", new ToolIntentBinding("scope", null)),
            "Found {result} in {scope}.");

        var descriptor = new ToolDescriptor(
            "search",
            "Search",
            "Searches one declared scope.",
            ImmutableArray.Create(
                new ToolInputDescriptor("query", "Search query.", ToolInputType.String),
                new ToolInputDescriptor("scope", "Search scope.", ToolInputType.String)),
            Intents: ImmutableArray.Create(definition));
        var resolver = new ToolIntentResolver(
        [
            new ManifestToolIntentRecognizer(descriptor, definition)
        ]);

        var resolution = resolver.Resolve("search a in b in c");

        Assert.Equal(ToolIntentResolutionStatus.NotRecognized, resolution.Status);
        Assert.Null(resolution.Intent);
    }

    [Fact]
    public void Reports_ambiguity_when_two_tools_declare_the_same_phrase()
    {
        var calculator = BundledToolTestSupport.CreateRegistry()
            .Descriptors
            .Single(descriptor => descriptor.Id == "calculator");
        var multiplyIntent = calculator.Intents.Single(
            definition => definition.Pattern == "what is {left:number} multiplied by {right:number}");
        var alternate = new ToolDescriptor(
            "alternate-calculator",
            "Alternate calculator",
            "A second capability declaring the same phrase.",
            calculator.Inputs,
            Intents: ImmutableArray.Create(multiplyIntent));
        var resolver = CreateResolver([calculator, alternate]);

        var resolution = resolver.Resolve("What is 12 multiplied by 3?");

        Assert.Equal(ToolIntentResolutionStatus.Ambiguous, resolution.Status);
        Assert.Null(resolution.Intent);
    }

    [Fact]
    public void Reports_unknown_language_without_selecting_a_capability()
    {
        var resolution = CreateResolver().Resolve("Tell me something interesting.");

        Assert.Equal(ToolIntentResolutionStatus.NotRecognized, resolution.Status);
        Assert.Null(resolution.Intent);
    }

    [Fact]
    public async Task Selects_and_invokes_a_new_tool_using_only_its_manifest_and_script()
    {
        var root = Path.Combine(Path.GetTempPath(), $"lattice-manifest-intent-{Guid.NewGuid():N}");
        var toolDirectory = Directory.CreateDirectory(Path.Combine(root, "echo")).FullName;

        try
        {
            File.WriteAllText(
                Path.Combine(toolDirectory, "main.py"),
                """
                import json
                import sys

                request = json.loads(sys.stdin.readline())
                response = {
                    "protocolVersion": 1,
                    "requestId": request["requestId"],
                    "success": True,
                    "result": request["arguments"]["message"]
                }
                sys.stdout.write(json.dumps(response) + "\n")
                """);

            var manifest = JsonSerializer.Serialize(new
            {
                protocolVersion = 1,
                id = "example.echo",
                name = "Echo",
                description = "Returns one supplied word.",
                command = "python",
                arguments = new[] { "main.py" },
                inputs = new[]
                {
                    new
                    {
                        name = "message",
                        description = "The text to echo.",
                        type = "string",
                        required = true
                    }
                },
                permissions = Array.Empty<string>(),
                intents = new[]
                {
                    new
                    {
                        pattern = "[please] echo {message:text} [to the console]",
                        arguments = new Dictionary<string, string>
                        {
                            ["message"] = "$message"
                        },
                        response = "Echoed: {result}."
                    }
                }
            });

            File.WriteAllText(Path.Combine(toolDirectory, "tool.json"), manifest);

            var discovered = StdioToolProvider.TryDiscover(root, out var tools, out var error);
            Assert.True(discovered, error);

            var registry = new ToolRegistry(
                tools,
                executionTimeout: TimeSpan.FromSeconds(10));
            var resolver = CreateResolver(registry.Descriptors);
            var resolution = resolver.Resolve(
                "Please echo lattice from a manifest-only phrase then a final token to the console.");

            Assert.True(resolution.IsResolved);
            Assert.Equal("example.echo", resolution.Intent!.ToolId);

            var invocation = await registry.TryInvokeAsync(
                resolution.Intent.ToolId,
                resolution.Intent.ArgumentsJson,
                TestContext.Current.CancellationToken);

            Assert.True(invocation.IsSuccess, invocation.Error);
            Assert.Equal(
                "Echoed: lattice from a manifest-only phrase then a final token.",
                resolution.Intent.FormatResponse(invocation.Observation!));

            var withoutOptionalPhrases = resolver.Resolve("Echo another multi word phrase.");
            Assert.True(withoutOptionalPhrases.IsResolved);

            var secondInvocation = await registry.TryInvokeAsync(
                withoutOptionalPhrases.Intent!.ToolId,
                withoutOptionalPhrases.Intent.ArgumentsJson,
                TestContext.Current.CancellationToken);

            Assert.True(secondInvocation.IsSuccess, secondInvocation.Error);
            Assert.Equal(
                "Echoed: another multi word phrase.",
                withoutOptionalPhrases.Intent.FormatResponse(secondInvocation.Observation!));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static ToolIntentResolver CreateResolver() =>
        CreateResolver(BundledToolTestSupport.CreateRegistry().Descriptors);

    private static ToolIntentResolver CreateResolver(
        IEnumerable<ToolDescriptor> descriptors)
    {
        var recognizers = descriptors
            .Where(descriptor => !descriptor.Intents.IsDefaultOrEmpty)
            .SelectMany(descriptor => descriptor.Intents.Select(
                definition => new ManifestToolIntentRecognizer(descriptor, definition)));

        return new ToolIntentResolver(recognizers);
    }

    private static void AssertInvocation(
        ToolIntent intent,
        decimal expectedLeft,
        string expectedOperation,
        decimal expectedRight)
    {
        Assert.Equal("calculator", intent.ToolId);

        using var arguments = JsonDocument.Parse(intent.ArgumentsJson);
        Assert.Equal(expectedLeft, arguments.RootElement.GetProperty("left").GetDecimal());
        Assert.Equal(expectedOperation, arguments.RootElement.GetProperty("operation").GetString());
        Assert.Equal(expectedRight, arguments.RootElement.GetProperty("right").GetDecimal());
    }
}
