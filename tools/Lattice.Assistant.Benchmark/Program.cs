using System.Text.Json;
using Lattice.Assistant.Benchmark;

if (!BenchmarkOptions.TryParse(args, out var options, out var error))
{
    Console.Error.WriteLine(error);
    Console.Error.WriteLine(BenchmarkOptions.Usage);
    return 64;
}

var parsedOptions = options!;

if (parsedOptions.ShowHelp)
{
    Console.WriteLine(BenchmarkOptions.Usage);
    return 0;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

try
{
    var report = await AssistantBenchmarkRunner.RunAsync(
        parsedOptions.ToRunOptions(),
        cancellation.Token,
        result => Console.WriteLine(
            $"[{result.RunStatus}] {result.Id} #{result.Repetition} ({result.LatencyMilliseconds:0} ms)"));
    Console.WriteLine(AssistantBenchmarkRunner.FormatSummary(report));
    Console.WriteLine($"JSON report: {Path.GetFullPath(parsedOptions.JsonOutputPath)}");
    Console.WriteLine($"Markdown report: {Path.GetFullPath(parsedOptions.MarkdownOutputPath)}");
    return parsedOptions.FailOnUnhandled && report.UnhandledRuns > 0 ? 1 : 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Benchmark cancelled. Partial JSON and Markdown reports were written to:");
    Console.Error.WriteLine(Path.GetFullPath(parsedOptions.JsonOutputPath));
    Console.Error.WriteLine(Path.GetFullPath(parsedOptions.MarkdownOutputPath));
    return 130;
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
{
    Console.Error.WriteLine($"Benchmark failed: {exception.Message}");
    return 2;
}
