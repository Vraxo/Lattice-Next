using Lattice.Client;
using Lattice.Core.Tools;
using Spectre.Console;

namespace Lattice.Repl;

internal sealed class ReplStatusCommand(
    ILatticeSession session,
    ReplToolAccess toolAccess,
    ReplTranslationCommands translationCommands)
{
    public async ValueTask<bool> TryHandleAsync(string input)
    {
        if (!string.Equals(input, ":status", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var status = await session.GetStatusAsync();

        AnsiConsole.MarkupLine("[bold]Implementation status[/]");
        AnsiConsole.MarkupLine("  [cyan]Language[/]             LCE 0.9 + native English grammar");
        AnsiConsole.MarkupLine("  [cyan]Semantics[/]            Terms, n-ary predicates, conjunction, existential and universal quantification");
        AnsiConsole.MarkupLine("  [cyan]Reasoning[/]            Unary and fixed-object relational inference + existential lookup");
        AnsiConsole.MarkupLine($"  [cyan]Session knowledge[/]    {status.FactCount} facts, {status.RuleCount} rules");
        AnsiConsole.MarkupLine($"  [cyan]Known individuals[/]    {status.KnownIndividualCount}");
        AnsiConsole.MarkupLine("  [cyan]Persistence[/]          [yellow]Current session only[/]");
        AnsiConsole.MarkupLine("  [cyan]Pack loading[/]         Version 1 JSON; imported claims retain pack provenance");
        AnsiConsole.MarkupLine("  [cyan]Provenance[/]            Structured sources; duplicate claims retain all sources");
        AnsiConsole.MarkupLine($"  [cyan]Knowledge packs[/]      {status.LoadedKnowledgePackCount} loaded");
        AnsiConsole.MarkupLine($"  [cyan]Tools[/]                {toolAccess.Registry.Descriptors.Count} registered");
        AnsiConsole.MarkupLine($"  [cyan]Translation[/]          {(translationCommands.TranslationService is null ? "disabled" : $"LM Studio · {translationCommands.Configuration.LmStudio.ModelKey}")}");
        AnsiConsole.WriteLine();
        return true;
    }
}
