using Lattice.Client;
using Spectre.Console;

namespace Lattice.Repl;

internal static class ReplKnowledgePackCommand
{
    public static async ValueTask<bool> TryHandleAsync(
        string input,
        ILatticeSession session)
    {
        if (!input.StartsWith(":load ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var path = input[6..].Trim().Trim('"');

        if (path.Length == 0)
        {
            AnsiConsole.MarkupLine("[bold yellow]Lattice[/] [dim]›[/] Usage: :load <path-to-pack.json>");
            AnsiConsole.WriteLine();
            return true;
        }

        if (!File.Exists(path))
        {
            AnsiConsole.MarkupLine("[bold red]Lattice[/] [dim]›[/] File not found.");
            AnsiConsole.WriteLine();
            return true;
        }

        string packJson;

        try
        {
            packJson = await File.ReadAllTextAsync(path);
        }
        catch (IOException)
        {
            AnsiConsole.MarkupLine("[bold red]Lattice[/] [dim]›[/] The pack file could not be read.");
            AnsiConsole.WriteLine();
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            AnsiConsole.MarkupLine("[bold red]Lattice[/] [dim]›[/] Access to the pack file was denied.");
            AnsiConsole.WriteLine();
            return true;
        }

        var result = await session.LoadKnowledgePackAsync(packJson);

        if (!result.IsSuccess)
        {
            AnsiConsole.Markup("[bold red]Lattice[/] [dim]›[/] ");
            AnsiConsole.WriteLine(result.Error ?? "The knowledge pack could not be loaded.");
            AnsiConsole.WriteLine();
            return true;
        }

        AnsiConsole.MarkupLine(
            $"[bold cyan]Lattice[/] [dim]› Loaded '{Markup.Escape(result.Name ?? "knowledge pack")}' ({result.ClaimCount} claims).[/]");
        AnsiConsole.WriteLine();
        return true;
    }
}
