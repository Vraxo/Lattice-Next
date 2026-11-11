using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lattice.Client;
using Lattice.Runtime;

namespace Lattice.Cli;

public static class LatticeCli
{
    private const int SuccessExitCode = 0;
    private const int ProcessingErrorExitCode = 2;
    private const int KnowledgePackErrorExitCode = 3;
    private const int UsageErrorExitCode = 64;
    private const int InputOutputErrorExitCode = 66;
    private const int CancelledExitCode = 130;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<int> RunAsync(
        string[] arguments,
        TextReader standardInput,
        TextWriter standardOutput,
        TextWriter standardError,
        bool standardInputIsRedirected,
        CancellationToken cancellationToken = default)
    {
        if (!CliOptions.TryParse(arguments, out var options, out var parseError))
        {
            standardError.WriteLine(parseError);
            standardError.WriteLine(GetUsage());
            return UsageErrorExitCode;
        }

        if (options!.ShowHelp)
        {
            standardOutput.WriteLine(GetUsage());
            return SuccessExitCode;
        }

        if (options.ShowVersion)
        {
            standardOutput.WriteLine(
                Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown");
            return SuccessExitCode;
        }

        if (options.InputFile is null &&
            options.InputText is null &&
            !standardInputIsRedirected)
        {
            standardError.WriteLine("No input was provided.");
            standardError.WriteLine(GetUsage());
            return UsageErrorExitCode;
        }

        var loadedPacks = new List<LatticeKnowledgePackResult>();
        Guid? sessionId = null;

        try
        {
            var input = await ReadInputAsync(
                options,
                standardInput,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(input))
            {
                standardError.WriteLine("Input is empty.");
                standardError.WriteLine(GetUsage());
                return UsageErrorExitCode;
            }

            var client = new LatticeRuntimeClient();
            await using var session = await client.CreateSessionAsync(cancellationToken);
            sessionId = session.SessionId;

            foreach (var packPath in options.KnowledgePackPaths)
            {
                var json = await File.ReadAllTextAsync(packPath, cancellationToken);
                var loaded = await session.LoadKnowledgePackAsync(json, cancellationToken);

                if (!loaded.IsSuccess)
                {
                    WriteOutput(
                        options,
                        standardOutput,
                        standardError,
                        false,
                        sessionId,
                        loadedPacks,
                        null,
                        loaded.Error ?? $"Knowledge pack '{packPath}' could not be loaded.");
                    return KnowledgePackErrorExitCode;
                }

                loadedPacks.Add(loaded);
            }

            var turn = await session.SubmitAsync(input, cancellationToken);
            var success = turn.IsSuccess && turn.Utterances.All(
                utterance => utterance.Kind is
                    LatticeUtteranceResultKind.StatementStored or
                    LatticeUtteranceResultKind.QuestionAnswered);

            WriteOutput(
                options,
                standardOutput,
                standardError,
                success,
                sessionId,
                loadedPacks,
                turn,
                turn.Error);

            return success ? SuccessExitCode : ProcessingErrorExitCode;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            standardError.WriteLine("Operation cancelled.");
            return CancelledExitCode;
        }
        catch (IOException exception)
        {
            WriteOutput(
                options,
                standardOutput,
                standardError,
                false,
                sessionId,
                loadedPacks,
                null,
                exception.Message);
            return InputOutputErrorExitCode;
        }
        catch (UnauthorizedAccessException exception)
        {
            WriteOutput(
                options,
                standardOutput,
                standardError,
                false,
                sessionId,
                loadedPacks,
                null,
                exception.Message);
            return InputOutputErrorExitCode;
        }
    }

    private static async Task<string> ReadInputAsync(
        CliOptions options,
        TextReader standardInput,
        CancellationToken cancellationToken)
    {
        if (options.InputFile is not null)
        {
            return await File.ReadAllTextAsync(options.InputFile, cancellationToken);
        }

        if (options.InputText is not null)
        {
            return options.InputText;
        }

        return await standardInput.ReadToEndAsync(cancellationToken);
    }

    private static void WriteOutput(
        CliOptions options,
        TextWriter standardOutput,
        TextWriter standardError,
        bool success,
        Guid? sessionId,
        IReadOnlyList<LatticeKnowledgePackResult> loadedPacks,
        LatticeTurnResult? turn,
        string? error)
    {
        if (options.Format == "json")
        {
            standardOutput.WriteLine(JsonSerializer.Serialize(
                new
                {
                    Success = success,
                    SessionId = sessionId,
                    LoadedPacks = loadedPacks,
                    Turn = turn,
                    Error = error
                },
                JsonOptions));
            return;
        }

        foreach (var pack in loadedPacks)
        {
            standardOutput.WriteLine($"Loaded knowledge pack: {pack.Name} ({pack.ClaimCount} claims).");
        }

        if (turn is not null)
        {
            CliTextFormatter.WriteTurn(turn, standardOutput, standardError);
        }

        if (turn is null && !string.IsNullOrWhiteSpace(error))
        {
            standardError.WriteLine(error);
        }
    }

    private static string GetUsage() =>
        """
        Lattice.Cli — non-interactive Lattice client

        Usage:
          lattice [options] <input>
          lattice [options] --input-file <path>
          command | lattice [options]

        Options:
          --load-pack <path>   Load a version 1 knowledge pack (repeatable).
          --input-file <path>  Read the turn from a file.
          --format <format>    Output format: json (default) or text.
          --help, -h           Show this help.
          --version            Show the assembly version.
          --                   Treat remaining arguments as input text.

        Exit codes:
          0    Turn completed; every utterance was handled.
          2    Input was rejected or an utterance needs clarification.
          3    A knowledge pack failed validation.
          64   Invalid command-line usage.
          66   An input or knowledge-pack file could not be read.
          130  Operation cancelled.
        """;
}
