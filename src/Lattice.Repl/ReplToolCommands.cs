using Lattice.Core.Context;
using Lattice.Core.Tools;
using Spectre.Console;

namespace Lattice.Repl;

internal sealed class ReplToolCommands(
    ReplToolAccess toolAccess,
    ConversationContext conversationContext,
    ReplTranslationCommands translationCommands)
{
    public async ValueTask<bool> TryHandleAsync(string input)
    {

        if (string.Equals(input, ":tools", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine("[bold]Available tools[/]");

            if (toolAccess.Registry.Descriptors.Count == 0)
            {
                AnsiConsole.MarkupLine(
                    "[yellow]No tools are registered. Check the configured tools directory and tool.json manifests.[/]");
            }

            foreach (var descriptor in toolAccess.Registry.Descriptors.OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                AnsiConsole.MarkupLine($"  [cyan]{descriptor.Id}[/] — {descriptor.Name}");
                AnsiConsole.MarkupLine($"    {descriptor.Description}");

                foreach (var argument in descriptor.Inputs)
                {
                    var required = argument.Required ? "required" : "optional";
                    AnsiConsole.MarkupLine($"    [dim]{argument.Name}: {argument.Type} ({required}) — {argument.Description}[/]");
                }
            }

            AnsiConsole.WriteLine();
            return true;
        }

        if (input.StartsWith(":tool ", StringComparison.OrdinalIgnoreCase))
        {
            var invocation = input[6..].Trim();
            var separator = invocation.IndexOf(' ');

            if (separator <= 0 || separator == invocation.Length - 1)
            {
                AnsiConsole.MarkupLine("[bold yellow]Lattice[/] [dim]›[/] Usage: :tool <tool-id> <arguments-json>");
                AnsiConsole.WriteLine();
                return true;
            }

            var toolId = invocation[..separator];
            var arguments = invocation[(separator + 1)..].Trim();
            var result = await toolAccess.InvokeAsync(toolId, arguments);
            translationCommands.UpdateConfiguration(toolAccess.Configuration);

            if (result is null)
            {
                AnsiConsole.WriteLine();
                return true;
            }

            if (!result.IsSuccess)
            {
                AnsiConsole.Markup("[bold red]Lattice[/] [dim]›[/] ");
                AnsiConsole.WriteLine(result.Error ?? "The tool invocation failed.");
                AnsiConsole.WriteLine();
                return true;
            }

            AnsiConsole.MarkupLine($"[bold cyan]Lattice[/] [dim]› Observation from {result.Observation!.ToolId}:[/]");
            AnsiConsole.WriteLine(result.Observation.Content);
            conversationContext.Observe(result.Observation!);
            AnsiConsole.MarkupLine("[dim]This result is an observation; it has not been added to session knowledge.[/]");
            AnsiConsole.WriteLine();
            return true;
        }

        return false;
    }
}
