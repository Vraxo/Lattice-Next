using System.Text.Json;
using Lattice.Assistant.Benchmark;
using Xunit;

namespace Lattice.Benchmark.Tests;

public sealed class AssistantBenchmarkRunnerTests
{
    [Fact]
    public async Task Runs_each_repetition_in_a_fresh_session_and_writes_both_reports()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"lattice-benchmark-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var benchmarkPath = Path.Combine(directory, "benchmark.json");
        var jsonPath = Path.Combine(directory, "report.json");
        var markdownPath = Path.Combine(directory, "report.md");
        await File.WriteAllTextAsync(
            benchmarkPath,
            """
            {
              "schema_version": 1,
              "title": "Runner test",
              "status": "test",
              "records": [
                {
                  "id": "known-fact",
                  "prompt": "Fido is a dog. Is Fido a dog?",
                  "category": "reasoning",
                  "evaluation_layer": "test"
                },
                {
                  "id": "unknown-concept",
                  "prompt": "What is a quark?",
                  "category": "knowledge",
                  "evaluation_layer": "test"
                }
              ]
            }
            """);

        try
        {
            var report = await AssistantBenchmarkRunner.RunAsync(
                new BenchmarkRunOptions(
                    benchmarkPath,
                    jsonPath,
                    markdownPath,
                    Repeat: 2));

            Assert.Equal(2, report.PromptCount);
            Assert.Equal(4, report.ExecutedRuns);
            Assert.Equal(4, report.HandledRuns);
            Assert.Equal(2, report.AnsweredRuns);
            Assert.Equal(0, report.UnhandledRuns);
            Assert.Equal(2, report.StablePromptCount);
            Assert.Equal(4, report.Results.Count);
            Assert.All(report.Results, result => Assert.Equal("Handled", result.RunStatus));
            Assert.Equal(
                2,
                report.Results.Count(result => result.UtteranceKinds.Contains("QuestionAnswered", StringComparer.Ordinal)));
            Assert.Equal(2, report.Categories.Sum(group => group.AnsweredRuns));

            var sessionIds = report.Results
                .Select(result =>
                {
                    using var output = JsonDocument.Parse(result.StandardOutput);
                    return output.RootElement.GetProperty("sessionId").GetString();
                })
                .ToArray();

            Assert.Equal(4, sessionIds.Distinct(StringComparer.Ordinal).Count());
            Assert.True(File.Exists(jsonPath));
            Assert.True(File.Exists(markdownPath));
            var markdown = await File.ReadAllTextAsync(markdownPath);
            Assert.Contains("Answered counts prompts that produced a", markdown);
            Assert.Contains("2/2 answered", markdown);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Applies_category_filters_before_running_prompts()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"lattice-benchmark-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var benchmarkPath = Path.Combine(directory, "benchmark.json");

        await File.WriteAllTextAsync(
            benchmarkPath,
            """
            {
              "schema_version": 1,
              "title": "Filter test",
              "status": "test",
              "records": [
                {
                  "id": "first",
                  "prompt": "Fido is a dog.",
                  "category": "facts",
                  "evaluation_layer": "test"
                },
                {
                  "id": "second",
                  "prompt": "Rex is a cat.",
                  "category": "statements",
                  "evaluation_layer": "test"
                }
              ]
            }
            """);

        try
        {
            var report = await AssistantBenchmarkRunner.RunAsync(
                new BenchmarkRunOptions(
                    benchmarkPath,
                    Path.Combine(directory, "report.json"),
                    Path.Combine(directory, "report.md"),
                    Category: "STATEMENTS"));

            Assert.Equal(1, report.PromptCount);
            Assert.Equal("second", Assert.Single(report.Results).Id);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Rejects_duplicate_prompt_ids_before_executing_anything()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"lattice-benchmark-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var benchmarkPath = Path.Combine(directory, "benchmark.json");
        await File.WriteAllTextAsync(
            benchmarkPath,
            """
            {
              "schema_version": 1,
              "title": "Invalid benchmark",
              "status": "test",
              "records": [
                {
                  "id": "duplicate",
                  "prompt": "Fido is a dog.",
                  "category": "facts",
                  "evaluation_layer": "test"
                },
                {
                  "id": "duplicate",
                  "prompt": "Rex is a cat.",
                  "category": "facts",
                  "evaluation_layer": "test"
                }
              ]
            }
            """);

        try
        {
            await Assert.ThrowsAsync<JsonException>(() =>
                AssistantBenchmarkRunner.RunAsync(
                    new BenchmarkRunOptions(
                        benchmarkPath,
                        Path.Combine(directory, "report.json"),
                        Path.Combine(directory, "report.md"))));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
