using Lattice.Core.Language;
using Lattice.Core.Semantics;
using Spectre.Console;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var parser = new LceParser();

AnsiConsole.MarkupLine("[bold cyan]Lattice[/]");
AnsiConsole.MarkupLine("[dim]Symbolic intelligence · LCE 0.1[/]");
AnsiConsole.MarkupLine("[dim]Enter a sentence, :help for examples, :status for implementation status, or :quit to exit.[/]");
AnsiConsole.WriteLine();

while (true)
{
    AnsiConsole.Markup("[bold green]you[/] [dim]›[/] ");

    var line = Console.ReadLine();

    if (line is null || string.Equals(line.Trim(), ":quit", StringComparison.OrdinalIgnoreCase))
    {
        AnsiConsole.MarkupLine("[dim]Session ended.[/]");
        break;
    }

    var input = line.Trim();

    if (input.Length == 0)
    {
        continue;
    }

    if (string.Equals(input, ":help", StringComparison.OrdinalIgnoreCase))
    {
        AnsiConsole.MarkupLine("[bold]Supported forms[/]");
        AnsiConsole.MarkupLine("  [green]Every dog is an animal.[/]");
        AnsiConsole.MarkupLine("  [green]Fido is a dog.[/]");
        AnsiConsole.MarkupLine("  [green]Is Fido an animal?[/]");
        AnsiConsole.MarkupLine("[dim]LCE 0.1 is a small controlled-language subset, not full ACE or unrestricted English.[/]");
        AnsiConsole.WriteLine();
        continue;
    }

    if (string.Equals(input, ":status", StringComparison.OrdinalIgnoreCase))
    {
        AnsiConsole.MarkupLine("[bold]Implementation status[/]");
        AnsiConsole.MarkupLine("  [cyan]Language[/]             LCE 0.1 parser");
        AnsiConsole.MarkupLine("  [cyan]Semantics[/]            Terms, predicates, implication, universal quantification");
        AnsiConsole.MarkupLine("  [cyan]Reasoning[/]            [yellow]Not implemented[/]");
        AnsiConsole.MarkupLine("  [cyan]Conversation memory[/] [yellow]Not implemented[/]");
        AnsiConsole.MarkupLine("  [cyan]Knowledge packs[/]      None loaded");
        AnsiConsole.MarkupLine("  [cyan]Tools[/]                None registered");
        AnsiConsole.WriteLine();
        continue;
    }

    var result = parser.Parse(input);

    if (result.Utterance is not { } utterance)
    {
        AnsiConsole.Markup("[bold red]Lattice[/] [dim]›[/] ");
        AnsiConsole.WriteLine(result.Error ?? "Input could not be parsed.");
        AnsiConsole.WriteLine();
        continue;
    }

    var kind = utterance.Kind == UtteranceKind.Question ? "question" : "assertion";
    AnsiConsole.MarkupLine($"[bold cyan]Lattice[/] [dim]› Parsed {kind}.[/]");
    AnsiConsole.Markup("[dim]meaning › [/]");
    AnsiConsole.WriteLine(SemanticFormatter.Format(utterance));
    AnsiConsole.WriteLine();
}
