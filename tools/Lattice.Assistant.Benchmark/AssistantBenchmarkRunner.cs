using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lattice.Cli;

namespace Lattice.Assistant.Benchmark;

public static class AssistantBenchmarkRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static async Task<BenchmarkRunReport> RunAsync(
        BenchmarkRunOptions options,
        CancellationToken cancellationToken = default,
        Action<BenchmarkRunResult>? onRunCompleted = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Repeat is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Repeat must be between 1 and 100.");
        }

        var benchmarkBytes = await File.ReadAllBytesAsync(options.BenchmarkPath, cancellationToken);
        var benchmark = JsonSerializer.Deserialize<BenchmarkDocument>(benchmarkBytes)
            ?? throw new JsonException("The benchmark document is empty.");

        if (benchmark.SchemaVersion != 1 || benchmark.Records.Count == 0)
        {
            throw new JsonException("Expected benchmark schema version 1 with at least one source record.");
        }

        if (benchmark.Records.Any(item =>
            string.IsNullOrWhiteSpace(item.Id) ||
            string.IsNullOrWhiteSpace(item.Prompt) ||
            string.IsNullOrWhiteSpace(item.Category) ||
            string.IsNullOrWhiteSpace(item.EvaluationLayer)))
        {
            throw new JsonException("Every benchmark record must have an ID, prompt, category, and evaluation layer.");
        }

        if (benchmark.Records.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != benchmark.Records.Count)
        {
            throw new JsonException("Benchmark record IDs must be unique.");
        }

        var selectedRecords = benchmark.Records
            .Where(item => options.Category is null ||
                string.Equals(item.Category, options.Category, StringComparison.OrdinalIgnoreCase))
            .Where(item => options.EvaluationLayer is null ||
                string.Equals(item.EvaluationLayer, options.EvaluationLayer, StringComparison.OrdinalIgnoreCase))
            .Where(item => options.Ids is null || options.Ids.Count == 0 || options.Ids.Contains(item.Id))
            .ToArray();

        if (selectedRecords.Length == 0)
        {
            throw new InvalidOperationException("No benchmark prompts matched the requested filters.");
        }

        var startedAt = DateTimeOffset.UtcNow;
        var timer = Stopwatch.StartNew();
        var results = new List<BenchmarkRunResult>(selectedRecords.Length * options.Repeat);

        var wasCancelled = false;

        try
        {
            for (var repetition = 1; repetition <= options.Repeat; repetition++)
            {
                foreach (var item in selectedRecords)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var result = await RunPromptAsync(item, repetition, cancellationToken);
                    results.Add(result);
                    onRunCompleted?.Invoke(result);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            wasCancelled = true;
        }

        timer.Stop();
        var handledRuns = results.Count(item => item.RunStatus == "Handled");
        var runnerErrorRuns = results.Count(item => item.RunStatus == "RunnerError");
        var categories = Summarize(results, item => item.Category);
        var layers = Summarize(results, item => item.EvaluationLayer);
        var stablePromptCount = options.Repeat < 2
            ? 0
            : results
                .GroupBy(item => item.Id, StringComparer.Ordinal)
                .Count(group =>
                    group.Count() == options.Repeat &&
                    group.All(item => item.OutcomeSignature is not null) &&
                    group.Select(item => item.OutcomeSignature).Distinct(StringComparer.Ordinal).Count() == 1);

        var report = new BenchmarkRunReport(
            startedAt,
            DateTimeOffset.UtcNow,
            Path.GetFullPath(options.BenchmarkPath),
            benchmark.Title,
            benchmark.Status,
            Convert.ToHexString(SHA256.HashData(benchmarkBytes)),
            "Lattice.Cli.RunAsync; in-process production CLI; fresh session for every prompt repetition",
            options.Repeat,
            selectedRecords.Length,
            results.Count,
            handledRuns,
            results.Count - handledRuns,
            runnerErrorRuns,
            stablePromptCount,
            results.Count == 0 ? 0 : results.Average(item => item.LatencyMilliseconds),
            categories,
            layers,
            results);

        await WriteReportAsync(report, options, CancellationToken.None);

        if (wasCancelled)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        return report;
    }

    public static string FormatSummary(BenchmarkRunReport report)
    {
        var stability = report.RepeatCount < 2
            ? "not measured (one repetition)"
            : $"{report.StablePromptCount}/{report.PromptCount} prompts stable across repetitions";

        return $"Completed {report.ExecutedRuns} runs across {report.PromptCount} prompts. " +
            $"{report.HandledRuns} handled; {report.UnhandledRuns} unhandled; " +
            $"{report.RunnerErrorRuns} runner errors. Stability: {stability}. " +
            $"Average latency: {report.AverageLatencyMilliseconds:0} ms.";
    }

    private static async Task<BenchmarkRunResult> RunPromptAsync(
        BenchmarkRecord item,
        int repetition,
        CancellationToken cancellationToken)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var timer = Stopwatch.StartNew();

        try
        {
            var arguments = new List<string> { "--format", "json" };

            foreach (var packPath in item.KnowledgePacks ?? Array.Empty<string>())
            {
                var benchmarkDirectory = Path.GetDirectoryName(Path.GetFullPath(options.BenchmarkPath))!;
                var resolvedPackPath = Path.GetFullPath(packPath, benchmarkDirectory);
                arguments.Add("--load-pack");
                arguments.Add(resolvedPackPath);
            }

            arguments.Add("--");
            arguments.Add(item.Prompt);

            var exitCode = await LatticeCli.RunAsync(
                arguments,
                new StringReader(string.Empty),
                output,
                error,
                standardInputIsRedirected: false,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            timer.Stop();

            using var document = JsonDocument.Parse(output.ToString());
            var root = document.RootElement;
            var cliSuccess = root.TryGetProperty("success", out var successElement) &&
                successElement.ValueKind == JsonValueKind.True;
            var turn = root.TryGetProperty("turn", out var turnElement) &&
                turnElement.ValueKind == JsonValueKind.Object
                    ? turnElement
                    : default;
            var turnStatus = GetString(turn, "status");
            var utteranceKinds = new List<string>();
            var responseParts = new List<string>();

            if (turn.ValueKind == JsonValueKind.Object &&
                turn.TryGetProperty("utterances", out var utterances) &&
                utterances.ValueKind == JsonValueKind.Array)
            {
                foreach (var utterance in utterances.EnumerateArray())
                {
                    var kind = GetString(utterance, "kind");
                    if (kind is not null)
                    {
                        utteranceKinds.Add(kind);
                    }

                    var message = GetString(utterance, "message");
                    var meaning = GetString(utterance, "meaning");
                    var truthValue = GetString(utterance, "truthValue");
                    var response = message ?? meaning ?? truthValue;

                    if (!string.IsNullOrWhiteSpace(response))
                    {
                        responseParts.Add(response);
                    }
                }
            }

            var runStatus = exitCode == 0 && cliSuccess ? "Handled" : "Rejected";
            var signature = turn.ValueKind == JsonValueKind.Object
                ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{exitCode}|{cliSuccess}|{turn.GetRawText()}")))
                : null;

            return new BenchmarkRunResult(
                item.Id,
                item.Prompt,
                item.Category,
                item.EvaluationLayer,
                repetition,
                exitCode,
                cliSuccess,
                runStatus,
                turnStatus,
                utteranceKinds,
                string.Join(" | ", responseParts),
                GetString(root, "error"),
                signature,
                timer.Elapsed.TotalMilliseconds,
                output.ToString(),
                error.ToString());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            timer.Stop();
            return new BenchmarkRunResult(
                item.Id,
                item.Prompt,
                item.Category,
                item.EvaluationLayer,
                repetition,
                -1,
                false,
                "RunnerError",
                null,
                Array.Empty<string>(),
                string.Empty,
                exception.Message,
                null,
                timer.Elapsed.TotalMilliseconds,
                output.ToString(),
                error.ToString());
        }
    }

    private static IReadOnlyList<BenchmarkGroupSummary> Summarize(
        IReadOnlyList<BenchmarkRunResult> results,
        Func<BenchmarkRunResult, string> keySelector) =>
        results
            .GroupBy(keySelector, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new BenchmarkGroupSummary(
                group.Key,
                group.Count(),
                group.Count(item => item.RunStatus == "Handled"),
                group.Count(item => item.RunStatus != "Handled"),
                group.Average(item => item.LatencyMilliseconds)))
            .ToArray();

    private static async Task WriteReportAsync(
        BenchmarkRunReport report,
        BenchmarkRunOptions options,
        CancellationToken cancellationToken)
    {
        var jsonDirectory = Path.GetDirectoryName(Path.GetFullPath(options.JsonOutputPath));
        var markdownDirectory = Path.GetDirectoryName(Path.GetFullPath(options.MarkdownOutputPath));

        if (!string.IsNullOrEmpty(jsonDirectory))
        {
            Directory.CreateDirectory(jsonDirectory);
        }

        if (!string.IsNullOrEmpty(markdownDirectory))
        {
            Directory.CreateDirectory(markdownDirectory);
        }

        await File.WriteAllTextAsync(
            options.JsonOutputPath,
            JsonSerializer.Serialize(report, JsonOptions),
            cancellationToken);
        await File.WriteAllTextAsync(
            options.MarkdownOutputPath,
            BuildMarkdown(report),
            cancellationToken);
    }

    private static string BuildMarkdown(BenchmarkRunReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Lattice assistant benchmark results");
        builder.AppendLine();
        builder.AppendLine($"> Benchmark: **{Escape(report.BenchmarkTitle)}** · status: `{Escape(report.BenchmarkStatus)}`");
        builder.AppendLine($"> Started: {report.StartedAtUtc:O} · completed: {report.CompletedAtUtc:O}");
        builder.AppendLine();
        builder.AppendLine("## Run summary");
        builder.AppendLine();
        builder.AppendLine($"- Prompts: {report.PromptCount}");
        builder.AppendLine($"- Repetitions per prompt: {report.RepeatCount}");
        builder.AppendLine($"- Executed runs: {report.ExecutedRuns}");
        builder.AppendLine($"- Handled: {report.HandledRuns}");
        builder.AppendLine($"- Unhandled: {report.UnhandledRuns}");
        builder.AppendLine($"- Runner errors: {report.RunnerErrorRuns}");
        builder.AppendLine($"- Average latency: {report.AverageLatencyMilliseconds:0} ms");
        builder.AppendLine($"- Stability: {(report.RepeatCount < 2 ? "not measured" : $"{report.StablePromptCount}/{report.PromptCount} prompts produced identical turn output across repetitions")}");
        builder.AppendLine();
        builder.AppendLine("**Important:** “Handled” means the CLI completed the turn without rejecting it. It does not establish factual accuracy, instruction satisfaction, or answer quality. Those require explicit evaluation criteria and, for open-ended prompts, human review.");
        builder.AppendLine();
        AppendGroupTable(builder, "By category", report.Categories);
        AppendGroupTable(builder, "By evaluation layer", report.EvaluationLayers);
        builder.AppendLine("## Prompt results");
        builder.AppendLine();
        builder.AppendLine("| ID | Category | Layer | Runs | Outcome | Stability |");
        builder.AppendLine("|---|---|---|---:|---|---|");

        foreach (var group in report.Results.GroupBy(item => item.Id, StringComparer.Ordinal))
        {
            var first = group.First();
            var handled = group.Count(item => item.RunStatus == "Handled");
            var signatures = group.Select(item => item.OutcomeSignature).Distinct(StringComparer.Ordinal).Count();
            var stability = report.RepeatCount < 2
                ? "not measured"
                : group.Count() < report.RepeatCount
                    ? "incomplete"
                    : signatures == 1 && group.All(item => item.OutcomeSignature is not null)
                        ? "stable"
                        : "varied";
            builder.AppendLine(
                $"| `{Escape(first.Id)}` | {Escape(first.Category)} | {Escape(first.EvaluationLayer)} | {group.Count()} | {handled}/{group.Count()} handled | {stability} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Individual outputs");
        foreach (var result in report.Results)
        {
            builder.AppendLine();
            builder.AppendLine($"### {Escape(result.Id)} · repetition {result.Repetition}");
            builder.AppendLine();
            builder.AppendLine($"**Prompt:** {Escape(result.Prompt)}");
            builder.AppendLine();
            builder.AppendLine($"- Category: {Escape(result.Category)}");
            builder.AppendLine($"- Layer: {Escape(result.EvaluationLayer)}");
            builder.AppendLine($"- Status: {result.RunStatus} (exit {result.ExitCode})");
            builder.AppendLine($"- Turn: {result.TurnStatus ?? "no turn result"}");
            builder.AppendLine($"- Utterances: {(result.UtteranceKinds.Count == 0 ? "none" : string.Join(", ", result.UtteranceKinds))}");
            builder.AppendLine($"- Latency: {result.LatencyMilliseconds:0} ms");
            if (!string.IsNullOrWhiteSpace(result.ResponseSummary))
            {
                builder.AppendLine();
                builder.AppendLine(Escape(result.ResponseSummary));
            }
            if (!string.IsNullOrWhiteSpace(result.Error))
            {
                builder.AppendLine();
                builder.AppendLine($"Error: {Escape(result.Error)}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Reproducibility");
        builder.AppendLine();
        builder.AppendLine($"- Execution: {Escape(report.ExecutionMode)}");
        builder.AppendLine($"- Benchmark SHA-256: `{report.BenchmarkSha256}`");
        builder.AppendLine("- Every run starts a fresh session; knowledge and conversational state do not carry between prompts or repetitions.");
        builder.AppendLine("- Full CLI JSON and standard-error output are retained in the companion JSON report.");
        return builder.ToString();
    }

    private static void AppendGroupTable(
        StringBuilder builder,
        string title,
        IReadOnlyList<BenchmarkGroupSummary> groups)
    {
        builder.AppendLine($"## {title}");
        builder.AppendLine();
        builder.AppendLine("| Group | Runs | Handled | Unhandled | Avg. latency |");
        builder.AppendLine("|---|---:|---:|---:|---:|");

        foreach (var group in groups)
        {
            builder.AppendLine(
                $"| {Escape(group.Name)} | {group.Runs} | {group.HandledRuns} | {group.UnhandledRuns} | {group.AverageLatencyMilliseconds:0} ms |");
        }

        builder.AppendLine();
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string Escape(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
}
