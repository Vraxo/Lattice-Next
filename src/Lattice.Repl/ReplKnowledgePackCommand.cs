using Lattice.Core.Reasoning;
using Spectre.Console;

namespace Lattice.Repl;

internal static class ReplKnowledgePackCommand
{
    public static bool TryHandle(
        string input,
        KnowledgePackLoader knowledgePackLoader,
        KnowledgeBase knowledgeBase,
        List<KnowledgePack> loadedPacks)
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
            packJson = File.ReadAllText(path);
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

        if (!knowledgePackLoader.TryLoadAndAdd(packJson, knowledgeBase, out var pack, out var error))
        {
            AnsiConsole.Markup("[bold red]Lattice[/] [dim]›[/] ");
            AnsiConsole.WriteLine(error ?? "The knowledge pack could not be loaded.");
            AnsiConsole.WriteLine();
            return true;
        }

        loadedPacks.Add(pack!);
        AnsiConsole.MarkupLine($"[bold cyan]Lattice[/] [dim]› Loaded '{pack!.Name}' ({pack.ClaimCount} claims).[/]");
        AnsiConsole.WriteLine();
        return true;
    }
}
