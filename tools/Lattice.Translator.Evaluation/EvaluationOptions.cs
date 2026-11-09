namespace Lattice.Translator.Evaluation;

public sealed record EvaluationOptions(
    string ConfigPath,
    string BenchmarkPath,
    string OutputPath,
    int Repeat,
    string? Category,
    bool ShowHelp)
{
    public const string Usage = """
        Lattice translator evaluation

        Usage:
          dotnet run --project tools/Lattice.Translator.Evaluation -- [options]

        Options:
          --config <path>       Lattice config (default: ./lattice.config.json)
          --benchmark <path>    Benchmark JSON (default: repository benchmark)
          --output <path>       JSON report (default: translator-evaluation-results.json)
          --repeat <count>      Repeat each case, 1-100 (default: 1)
          --category <name>     Run one benchmark category
          --help                Show this help

        Uses the configured LM Studio model. Never accepts proposals or changes the knowledge base.
        """;

    public static (EvaluationOptions? Options, string? Error) Parse(string[] args)
    {
        var config = Path.GetFullPath("lattice.config.json");
        var benchmark = FindBenchmark();
        var output = Path.GetFullPath("translator-evaluation-results.json");
        var repeat = 1;
        string? category = null;
        var help = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg is "--help" or "-h")
            {
                help = true;
                continue;
            }

            if (i + 1 >= args.Length)
            {
                return (null, $"Missing value for {arg}.");
            }

            var value = args[++i];
            switch (arg)
            {
                case "--config": config = Path.GetFullPath(value); break;
                case "--benchmark": benchmark = Path.GetFullPath(value); break;
                case "--output": output = Path.GetFullPath(value); break;
                case "--category":
                    if (string.IsNullOrWhiteSpace(value)) return (null, "--category cannot be empty.");
                    category = value;
                    break;
                case "--repeat":
                    if (!int.TryParse(value, out repeat) || repeat is < 1 or > 100)
                        return (null, "--repeat must be between 1 and 100.");
                    break;
                default: return (null, $"Unknown option: {arg}");
            }
        }

        return (new EvaluationOptions(config, benchmark, output, repeat, category, help), null);
    }

    private static string FindBenchmark()
    {
        for (var directory = new DirectoryInfo(Environment.CurrentDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "tests", "Lattice.Core.Tests",
                "Intelligence", "translator-benchmark.json");
            if (File.Exists(candidate)) return candidate;
        }

        return Path.GetFullPath(Path.Combine("tests", "Lattice.Core.Tests", "Intelligence", "translator-benchmark.json"));
    }
}
