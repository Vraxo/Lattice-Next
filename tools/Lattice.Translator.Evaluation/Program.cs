using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Lattice.Core.Intelligence;
using Lattice.Translators.LmStudio;
using Lattice.Translator.Evaluation;

var parsedOptions = EvaluationOptions.Parse(args);
if (parsedOptions.Options is null)
{
    Console.Error.WriteLine(parsedOptions.Error);
    Console.Error.WriteLine(EvaluationOptions.Usage);
    return 2;
}

var options = parsedOptions.Options;
if (options.ShowHelp)
{
    Console.WriteLine(EvaluationOptions.Usage);
    return 0;
}

if (!File.Exists(options.ConfigPath))
{
    Console.Error.WriteLine($"Configuration file not found: {options.ConfigPath}. Run :translator setup first.");
    return 2;
}

var store = new LmStudioConfigurationStore(options.ConfigPath);
if (!store.TryLoad(out var latticeConfiguration, out var configError))
{
    Console.Error.WriteLine(configError);
    return 2;
}

var configuration = latticeConfiguration.LmStudio;
if (string.IsNullOrWhiteSpace(configuration.ModelKey))
{
    Console.Error.WriteLine("No LM Studio model is configured. Run :translator setup first.");
    return 2;
}

BenchmarkDocument benchmark;
byte[] benchmarkBytes;
try
{
    benchmarkBytes = await File.ReadAllBytesAsync(options.BenchmarkPath);
    benchmark = JsonSerializer.Deserialize<BenchmarkDocument>(benchmarkBytes)
        ?? throw new JsonException("The benchmark document is empty.");
}
catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"Could not load benchmark: {exception.Message}");
    return 2;
}

if (benchmark.SchemaVersion != 2 ||
    benchmark.TranslationDirection != "natural-language-to-lce" ||
    benchmark.Cases.Count == 0)
{
    Console.Error.WriteLine("Expected benchmark schema version 2 with natural-language-to-LCE cases.");
    return 2;
}

var cases = benchmark.Cases.Where(item => options.Category is null ||
    string.Equals(item.Category, options.Category, StringComparison.OrdinalIgnoreCase)).ToArray();
if (cases.Length == 0)
{
    Console.Error.WriteLine($"No cases found for category '{options.Category}'.");
    return 2;
}

using var httpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
var runtime = new LmStudioRuntime(httpClient, configuration);
var translator = new LmStudioSemanticsTranslator(
    httpClient, configuration, runtime,
    reportStatus: message => Console.WriteLine($"LM Studio: {message}"));
var service = new SemanticTranslationService(translator);
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

var results = new List<EvaluationRecord>();
var startedAt = DateTimeOffset.UtcNow;
var stopwatch = Stopwatch.StartNew();
var benchmarkHash = Convert.ToHexString(SHA256.HashData(benchmarkBytes));

Console.WriteLine($"Model: {configuration.ModelIdentifier ?? configuration.ModelKey}");
Console.WriteLine($"Reasoning: {configuration.Reasoning}; temperature: 0");
Console.WriteLine($"Benchmark: {options.BenchmarkPath}");
Console.WriteLine($"Cases: {cases.Length}; repetitions: {options.Repeat}");
Console.WriteLine();

try
{
    for (var repetition = 1; repetition <= options.Repeat; repetition++)
    {
        foreach (var item in cases)
        {
            cancellation.Token.ThrowIfCancellationRequested();
            var caseTimer = Stopwatch.StartNew();
            var result = await service.TranslateAsync(item.Input, cancellation.Token);
            caseTimer.Stop();
            var record = EvaluationRecord.Create(item, repetition, result, caseTimer.Elapsed.TotalMilliseconds);
            results.Add(record);
            Console.WriteLine(
                $"[{(record.Passed ? "PASS" : "FAIL")}] {item.Id} #{repetition} " +
                $"({record.LatencyMilliseconds:0} ms): {record.ObservedOutcome}");
            if (!record.Passed) Console.WriteLine($"       {record.Detail} {record.Message}");
        }
    }
}
catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
{
    Console.WriteLine("Cancelled; writing partial results.");
}

stopwatch.Stop();
var supported = results.Where(item => item.ExpectedMeanings.Count > 0).ToArray();
var unsupported = results.Where(item => item.ExpectedMeanings.Count == 0).ToArray();
var report = new
{
    startedAtUtc = startedAt,
    completedAtUtc = DateTimeOffset.UtcNow,
    modelKey = configuration.ModelKey,
    modelIdentifier = configuration.ModelIdentifier ?? configuration.ModelKey,
    apiUrl = configuration.ApiUrl,
    reasoning = configuration.Reasoning,
    temperature = 0.0,
    maxOutputTokens = 320,
    benchmarkSha256 = benchmarkHash,
    repetitions = options.Repeat,
    total = results.Count,
    passed = results.Count(item => item.Passed),
    failed = results.Count(item => !item.Passed),
    supportedCases = supported.Length,
    exactSupportedMatches = supported.Count(item => item.Passed),
    unsupportedCases = unsupported.Length,
    safeAbstentions = unsupported.Count(item => item.Passed),
    averageLatencyMilliseconds = results.Count == 0 ? 0 : results.Average(item => item.LatencyMilliseconds),
    totalElapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
    results
};

var outputDirectory = Path.GetDirectoryName(options.OutputPath);
if (!string.IsNullOrEmpty(outputDirectory)) Directory.CreateDirectory(outputDirectory);
await File.WriteAllTextAsync(options.OutputPath, JsonSerializer.Serialize(report, new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true
}));

Console.WriteLine();
Console.WriteLine($"Results: {report.passed}/{report.total} passed; {report.failed} failed.");
Console.WriteLine($"Supported exact matches: {report.exactSupportedMatches}/{report.supportedCases}");
Console.WriteLine($"Safe abstentions: {report.safeAbstentions}/{report.unsupportedCases}");
Console.WriteLine($"Average latency: {report.averageLatencyMilliseconds:0} ms");
Console.WriteLine($"Total elapsed: {report.totalElapsedMilliseconds:0} ms");
Console.WriteLine($"JSON report: {Path.GetFullPath(options.OutputPath)}");
return cancellation.IsCancellationRequested || report.failed > 0 ? 1 : 0;
