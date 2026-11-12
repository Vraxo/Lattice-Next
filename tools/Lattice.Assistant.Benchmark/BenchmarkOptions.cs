namespace Lattice.Assistant.Benchmark;

internal sealed record BenchmarkOptions(
    string BenchmarkPath,
    string JsonOutputPath,
    string MarkdownOutputPath,
    int Repeat,
    string? Category,
    string? EvaluationLayer,
    IReadOnlySet<string> Ids,
    bool FailOnUnhandled,
    bool ShowHelp)
{
    public const string Usage = """
        Lattice assistant benchmark

        Usage:
          dotnet run --project tools/Lattice.Assistant.Benchmark -- [options]

        Options:
          --benchmark <path>  Benchmark JSON (default: data/benchmarks/assistant-benchmark-v1.json).
          --json <path>       JSON report (default: artifacts/assistant-benchmark-results.json).
          --markdown <path>   Markdown report (default: artifacts/assistant-benchmark-results.md).
          --repeat <count>    Fresh-session repetitions per prompt (default: 1).
          --category <name>   Run only one exact category (case-insensitive).
          --layer <name>      Run only one evaluation layer (case-insensitive).
          --id <id>           Run only this prompt ID; repeatable.
          --fail-on-unhandled Exit 1 if Lattice rejects or cannot handle any prompt.
          --help, -h          Show this help.

        Each repetition uses a new Lattice CLI session. Reports distinguish successful
        processing from answer correctness; no correctness score is claimed without
        explicit expected-answer criteria.
        """;

    public static bool TryParse(
        IReadOnlyList<string> arguments,
        out BenchmarkOptions? options,
        out string? error)
    {
        options = null;
        error = null;
        var benchmarkPath = "data/benchmarks/assistant-benchmark-v1.json";
        var jsonOutputPath = "artifacts/assistant-benchmark-results.json";
        var markdownOutputPath = "artifacts/assistant-benchmark-results.md";
        var repeat = 1;
        string? category = null;
        string? evaluationLayer = null;
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var failOnUnhandled = false;
        var showHelp = false;

        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];

            if (argument is "--help" or "-h")
            {
                showHelp = true;
                continue;
            }

            if (argument == "--fail-on-unhandled")
            {
                failOnUnhandled = true;
                continue;
            }

            if (argument is not ("--benchmark" or "--json" or "--markdown" or "--repeat" or "--category" or "--layer" or "--id"))
            {
                error = $"Unknown option '{argument}'.";
                return false;
            }

            if (index + 1 >= arguments.Count || string.IsNullOrWhiteSpace(arguments[index + 1]))
            {
                error = $"Option '{argument}' requires a value.";
                return false;
            }

            var value = arguments[++index];
            switch (argument)
            {
                case "--benchmark":
                    benchmarkPath = value;
                    break;
                case "--json":
                    jsonOutputPath = value;
                    break;
                case "--markdown":
                    markdownOutputPath = value;
                    break;
                case "--repeat":
                    if (!int.TryParse(value, out repeat) || repeat < 1 || repeat > 100)
                    {
                        error = "The --repeat value must be between 1 and 100.";
                        return false;
                    }
                    break;
                case "--category":
                    category = value;
                    break;
                case "--layer":
                    evaluationLayer = value;
                    break;
                case "--id":
                    ids.Add(value);
                    break;
            }
        }

        options = new BenchmarkOptions(
            benchmarkPath,
            jsonOutputPath,
            markdownOutputPath,
            repeat,
            category,
            evaluationLayer,
            ids,
            failOnUnhandled,
            showHelp);
        return true;
    }

    public BenchmarkRunOptions ToRunOptions() => new(
        BenchmarkPath,
        JsonOutputPath,
        MarkdownOutputPath,
        Repeat,
        Category,
        EvaluationLayer,
        Ids);
}
