using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lattice.Client;
using Lattice.Runtime;

const string benchmarkPath = "data/benchmarks/coding-benchmark-v1.json";
var outputDirectory = Path.Combine("artifacts");
Directory.CreateDirectory(outputDirectory);

if (!File.Exists(benchmarkPath))
{
    Console.Error.WriteLine($"Benchmark definition not found: {benchmarkPath}");
    return 2;
}

var benchmark = JsonSerializer.Deserialize<BenchmarkDefinition>(
    await File.ReadAllTextAsync(benchmarkPath),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

if (benchmark is null)
{
    Console.Error.WriteLine("Benchmark definition could not be read.");
    return 2;
}

var tasks = benchmark.Stages
    .Where(stage => stage.Id is "python-fundamentals" or "compositional-python")
    .SelectMany(stage => stage.Items.Select(item => new BenchmarkTask(
        stage.Id,
        stage.Name,
        item.Id,
        item.Prompt,
        item.ExecutionRequired,
        item.ExecutionFiles)))
    .ToArray();

var results = new List<TaskResult>(tasks.Length);
var client = new LatticeRuntimeClient();

foreach (var task in tasks)
{
    var result = await EvaluateAsync(client, task);
    results.Add(result);
    Console.WriteLine($"[{result.Status}] {task.Id}");
}

var report = new BenchmarkReport(
    benchmark.Id,
    DateTimeOffset.UtcNow,
    tasks.Length,
    results.Count(result => result.GeneratedCode is not null),
    results.Count(result => result.SyntaxValid == true),
    results.Count(result => result.ExecutionExitCode == 0),
    "Generation, syntax, and smoke-execution evidence only. No semantic score is assigned; tasks needing input fixtures or human review remain unscored.",
    results);

var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
await File.WriteAllTextAsync(
    Path.Combine(outputDirectory, "coding-benchmark-python-results.json"),
    JsonSerializer.Serialize(report, jsonOptions));
await File.WriteAllTextAsync(
    Path.Combine(outputDirectory, "coding-benchmark-python-results.md"),
    BuildMarkdown(report));

Console.WriteLine();
Console.WriteLine($"Python tasks: {report.TaskCount}");
Console.WriteLine($"Code generated: {report.GeneratedCount}/{report.TaskCount}");
Console.WriteLine($"Syntax valid: {report.SyntaxValidCount}/{report.TaskCount}");
Console.WriteLine($"Smoke executed: {report.ExecutionSucceededCount}/{report.TaskCount}");
Console.WriteLine("Semantic score: not assigned");
Console.WriteLine("Reports: artifacts/coding-benchmark-python-results.{json,md}");
return 0;

static async Task<TaskResult> EvaluateAsync(
    LatticeRuntimeClient client,
    BenchmarkTask task)
{
    await using var session = await client.CreateSessionAsync();
    // The benchmark stage supplies language context; keep each task prompt verbatim
    // while making that shared context explicit to the language parser.
    var submissionPrompt = task.StageId is "python-fundamentals" or "compositional-python"
        ? $"Write a Python program that fulfills this request: {task.Prompt}"
        : task.Prompt;
    var turn = await session.SubmitAsync(submissionPrompt);
    var utterance = turn.Utterances.FirstOrDefault();
    if (turn.Status != LatticeTurnStatus.Completed ||
        utterance?.Kind != LatticeUtteranceResultKind.TaskCompleted)
    {
        return new TaskResult(
            task.Id,
            task.StageId,
            task.Prompt,
            "generation-unhandled",
            utterance?.Message ?? turn.Error ?? "No completed task response.",
            null,
            null,
            null,
            null);
    }

    var code = ExtractPython(utterance.Message ?? string.Empty);
    if (code is null)
    {
        return new TaskResult(
            task.Id,
            task.StageId,
            task.Prompt,
            "no-python-block",
            utterance.Message,
            null,
            null,
            null,
            null);
    }

    var execution = await CheckPythonAsync(code, task.ExecutionFiles);
    return new TaskResult(
        task.Id,
        task.StageId,
        task.Prompt,
        execution.Status,
        null,
        code,
        execution.SyntaxValid,
        execution.ExitCode,
        execution.Output);
}

static string? ExtractPython(string message)
{
    var lines = message.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
    for (var index = 0; index < lines.Length; index++)
    {
        if (lines[index].Trim() != "```python")
        {
            continue;
        }

        var end = Array.IndexOf(lines, "```", index + 1);
        if (end <= index)
        {
            return null;
        }

        return string.Join(Environment.NewLine, lines[(index + 1)..end]);
    }

    return null;
}

static async Task<PythonCheck> CheckPythonAsync(string code, IReadOnlyDictionary<string, string>? executionFiles)
{
    var directory = Path.Combine(Path.GetTempPath(), "lattice-coding-benchmark-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    var scriptPath = Path.Combine(directory, "generated.py");
    await File.WriteAllTextAsync(scriptPath, code, new UTF8Encoding(false));

    try
    {
        if (executionFiles is not null)
        {
            foreach (var (relativePath, content) in executionFiles)
            {
                var fixturePath = Path.GetFullPath(Path.Combine(directory, relativePath));
                var directoryPrefix = directory.EndsWith(Path.DirectorySeparatorChar)
                    ? directory
                    : directory + Path.DirectorySeparatorChar;
                if (!fixturePath.StartsWith(directoryPrefix, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Execution fixture path must stay inside the temporary directory: {relativePath}");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(fixturePath)!);
                await File.WriteAllTextAsync(fixturePath, content, new UTF8Encoding(false));
            }
        }


    {
        var python = OperatingSystem.IsWindows() ? "python" : "python3";
        var syntax = await RunProcessAsync(python, ["-m", "py_compile", scriptPath], directory);
        if (syntax.ExitCode is null)
        {
            return new PythonCheck("python-unavailable", null, null, syntax.Output);
        }

        if (syntax.ExitCode != 0)
        {
            return new PythonCheck("syntax-invalid", false, syntax.ExitCode, syntax.Output);
        }

        var execution = await RunProcessAsync(python, [scriptPath], directory);
        if (execution.ExitCode is null)
        {
            return new PythonCheck("python-unavailable", true, null, execution.Output);
        }

        return new PythonCheck(
            execution.ExitCode == 0 ? "smoke-executed" : "execution-needs-review",
            true,
            execution.ExitCode,
            execution.Output);
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static async Task<ProcessResult> RunProcessAsync(
    string executable,
    IReadOnlyList<string> arguments,
    string workingDirectory)
{
    var startInfo = new ProcessStartInfo
    {
        FileName = executable,
        WorkingDirectory = workingDirectory,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };

    foreach (var argument in arguments)
    {
        startInfo.ArgumentList.Add(argument);
    }

    try
    {
        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return new ProcessResult(null, "Could not start Python.");
        }

        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            return new ProcessResult(null, "Python process exceeded the 15-second timeout.");
        }

        var output = (await stdout) + (await stderr);
        return new ProcessResult(process.ExitCode, output.Trim());
    }
    catch (System.ComponentModel.Win32Exception exception)
    {
        return new ProcessResult(null, exception.Message);
    }
}

static string BuildMarkdown(BenchmarkReport report)
{
    var builder = new StringBuilder();
    builder.AppendLine("# Lattice Python coding benchmark");
    builder.AppendLine();
    builder.AppendLine($"- Evaluated: {report.EvaluatedAt:O}");
    builder.AppendLine($"- Tasks: {report.TaskCount}");
    builder.AppendLine($"- Code generated: {report.GeneratedCount}/{report.TaskCount}");
    builder.AppendLine($"- Syntax valid: {report.SyntaxValidCount}/{report.TaskCount}");
    builder.AppendLine($"- Smoke execution succeeded: {report.ExecutionSucceededCount}/{report.TaskCount}");
    builder.AppendLine();
    builder.AppendLine($"> {report.ScoringNote}");
    builder.AppendLine();
    builder.AppendLine("| Task | Stage | Status | Syntax | Exit code |");
    builder.AppendLine("|---|---|---|---:|---:|");
    foreach (var result in report.Results)
    {
        builder.AppendLine($"| {result.Id} | {result.StageId} | {result.Status} | {result.SyntaxValid?.ToString() ?? "—"} | {result.ExecutionExitCode?.ToString() ?? "—"} |");
    }

    foreach (var result in report.Results)
    {
        builder.AppendLine();
        builder.AppendLine($"## {result.Id}");
        builder.AppendLine();
        builder.AppendLine($"**Prompt:** {result.Prompt}");
        builder.AppendLine();
        if (result.GeneratedCode is not null)
        {
            builder.AppendLine("```python");
            builder.AppendLine(result.GeneratedCode);
            builder.AppendLine("```");
        }
        else
        {
            builder.AppendLine(result.Response ?? "No response.");
        }

        if (!string.IsNullOrWhiteSpace(result.ExecutionOutput))
        {
            builder.AppendLine();
            builder.AppendLine("**Python output / diagnostic:**");
            builder.AppendLine();
            builder.AppendLine("```text");
            builder.AppendLine(result.ExecutionOutput);
            builder.AppendLine("```");
        }
    }

    return builder.ToString();
}

sealed record BenchmarkDefinition(string Id, BenchmarkStage[] Stages);
sealed record BenchmarkStage(string Id, string Name, BenchmarkItem[] Items);
sealed record BenchmarkItem(
    string Id,
    string Prompt,
    bool ExecutionRequired,
    Dictionary<string, string>? ExecutionFiles = null);
sealed record BenchmarkTask(
    string StageId,
    string StageName,
    string Id,
    string Prompt,
    bool ExecutionRequired,
    Dictionary<string, string>? ExecutionFiles);
sealed record PythonCheck(string Status, bool? SyntaxValid, int? ExitCode, string Output);
sealed record ProcessResult(int? ExitCode, string Output);
sealed record TaskResult(
    string Id,
    string StageId,
    string Prompt,
    string Status,
    string? Response,
    string? GeneratedCode,
    bool? SyntaxValid,
    int? ExecutionExitCode,
    string? ExecutionOutput);
sealed record BenchmarkReport(
    string BenchmarkId,
    DateTimeOffset EvaluatedAt,
    int TaskCount,
    int GeneratedCount,
    int SyntaxValidCount,
    int ExecutionSucceededCount,
    string ScoringNote,
    IReadOnlyList<TaskResult> Results);
