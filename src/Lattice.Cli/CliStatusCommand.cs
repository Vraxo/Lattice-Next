using Lattice.Core.Context;
using Lattice.Core.Reasoning;
using Spectre.Console;

namespace Lattice.Cli;

internal sealed class CliStatusCommand(
    KnowledgeBase knowledgeBase,
    ConversationContext conversationContext,
    List<KnowledgePack> loadedPacks,
    CliToolAccess toolAccess,
    CliTranslationCommands translationCommands)
{
    public bool TryHandle(string input)
    {

        if (string.Equals(input, ":status", StringComparison.OrdinalIgnoreCase))
                {
                    AnsiConsole.MarkupLine("[bold]Implementation status[/]");
                    AnsiConsole.MarkupLine("  [cyan]Language[/]             LCE 0.9 + native English grammar");
                    AnsiConsole.MarkupLine("  [cyan]Semantics[/]            Terms, n-ary predicates, conjunction, existential and universal quantification");
                    AnsiConsole.MarkupLine("  [cyan]Reasoning[/]            Unary and fixed-object relational inference + existential lookup");
                    AnsiConsole.MarkupLine($"  [cyan]Session knowledge[/]    {knowledgeBase.Facts.Count} facts, {knowledgeBase.Rules.Count} rules");
                    AnsiConsole.MarkupLine($"  [cyan]Known individuals[/]    {conversationContext.KnownIndividualCount}");
                    AnsiConsole.MarkupLine("  [cyan]Persistence[/]          [yellow]Current session only[/]");
                    AnsiConsole.MarkupLine("  [cyan]Pack loading[/]         Version 1 JSON; imported claims retain pack provenance");
                    AnsiConsole.MarkupLine("  [cyan]Provenance[/]            Structured sources; duplicate claims retain all sources");
                    AnsiConsole.MarkupLine($"  [cyan]Knowledge packs[/]      {loadedPacks.Count} loaded");
                    AnsiConsole.MarkupLine($"  [cyan]Tools[/]                {toolAccess.Registry.Descriptors.Count} registered");
                    AnsiConsole.MarkupLine($"  [cyan]Translation[/]          {(translationCommands.TranslationService is null ? "disabled" : $"LM Studio · {translationCommands.Configuration.LmStudio.ModelKey}")}");
                    AnsiConsole.WriteLine();
                    return true;
                }

        return false;
    }
}
