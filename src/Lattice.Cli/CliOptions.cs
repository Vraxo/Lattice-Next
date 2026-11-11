using System.Collections.Immutable;

namespace Lattice.Cli;

internal sealed record CliOptions(
    string Format,
    string? InputFile,
    ImmutableArray<string> KnowledgePackPaths,
    string? InputText,
    bool ShowHelp,
    bool ShowVersion)
{
    public static bool TryParse(
        IReadOnlyList<string> arguments,
        out CliOptions? options,
        out string? error)
    {
        options = null;
        error = null;

        var format = "json";
        string? inputFile = null;
        var knowledgePackPaths = ImmutableArray.CreateBuilder<string>();
        var positional = new List<string>();
        var positionalOnly = false;
        var showHelp = false;
        var showVersion = false;

        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];

            if (positionalOnly)
            {
                positional.Add(argument);
                continue;
            }

            if (argument == "--")
            {
                positionalOnly = true;
                continue;
            }

            switch (argument)
            {
                case "--help":
                case "-h":
                    showHelp = true;
                    break;

                case "--version":
                    showVersion = true;
                    break;

                case "--format":
                    if (!TryReadValue(arguments, ref index, out var requestedFormat))
                    {
                        error = "The --format option requires either 'json' or 'text'.";
                        return false;
                    }

                    if (requestedFormat is not ("json" or "text"))
                    {
                        error = $"Unsupported output format '{requestedFormat}'. Use 'json' or 'text'.";
                        return false;
                    }

                    format = requestedFormat;
                    break;

                case "--input-file":
                    if (!TryReadValue(arguments, ref index, out var requestedInputFile))
                    {
                        error = "The --input-file option requires a path.";
                        return false;
                    }

                    if (inputFile is not null)
                    {
                        error = "The --input-file option can only be specified once.";
                        return false;
                    }

                    inputFile = requestedInputFile;
                    break;

                case "--load-pack":
                    if (!TryReadValue(arguments, ref index, out var packPath))
                    {
                        error = "The --load-pack option requires a path.";
                        return false;
                    }

                    knowledgePackPaths.Add(packPath);
                    break;

                default:
                    if (argument.StartsWith("-", StringComparison.Ordinal))
                    {
                        error = $"Unknown option '{argument}'.";
                        return false;
                    }

                    positional.Add(argument);
                    break;
            }
        }

        if (inputFile is not null && positional.Count > 0)
        {
            error = "Provide input either positionally or with --input-file, not both.";
            return false;
        }

        options = new CliOptions(
            format,
            inputFile,
            knowledgePackPaths.ToImmutable(),
            positional.Count == 0 ? null : string.Join(" ", positional),
            showHelp,
            showVersion);
        return true;
    }

    private static bool TryReadValue(
        IReadOnlyList<string> arguments,
        ref int index,
        out string value)
    {
        value = string.Empty;

        if (index + 1 >= arguments.Count || string.IsNullOrWhiteSpace(arguments[index + 1]))
        {
            return false;
        }

        value = arguments[++index];
        return true;
    }
}
