using System.Text.Json;
using Lattice.Assistant.Benchmark;
using Xunit;

namespace Lattice.Benchmark.Tests;

public sealed class BenchmarkKnowledgePackTests
{
    [Fact]
    public async Task Loads_record_knowledge_packs_before_submitting_its_prompt()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"lattice-benchmark-packs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var packPath = Path.Combine(directory, "geometry.json");
        var benchmarkPath = Path.Combine(directory, "benchmark.json");

        await File.WriteAllTextAsync(
            packPath,
            """
            {
              "formatVersion": 1,
              "id": "geometry-test",
              "name": "Geometry test",
              "claims": [
                "Polygon is a geometric-shape.",
                "Every polygon has a closed boundary."
              ]
            }
            """);

        await File.WriteAllTextAsync(
            benchmarkPath,
            JsonSerializer.Serialize(new
            {
                schema_version = 1,
                title = "Knowledge pack test",
                status = "test",
                records = new[]
                {
                    new
                    {
                        id = "polygon-definition",
                        prompt = "What is a polygon?",
                        category = "definition",
                        evaluation_layer = "test",
                        knowledge_packs = new[] { packPath }
                    }
                }
            }));

        try
        {
            var report = await AssistantBenchmarkRunner.RunAsync(
                new BenchmarkRunOptions(
                    benchmarkPath,
                    Path.Combine(directory, "report.json"),
                    Path.Combine(directory, "report.md")));

            var result = Assert.Single(report.Results);
            Assert.Equal("Handled", result.RunStatus);
            Assert.Contains("QuestionAnswered", result.UtteranceKinds);
            Assert.Contains("geometric shape", result.ResponseSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("boundary", result.ResponseSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("closed", result.ResponseSummary, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
