using Lattice.Client;
using Spectre.Console;

namespace Lattice.Repl;

internal sealed class ReplObjectiveExplanationCommand(ILatticeSession session)
{
    public async ValueTask<bool> TryHandleAsync(string input)
    {
        if (!string.Equals(input, ":why", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var status = await session.GetStatusAsync();
        if (string.IsNullOrWhiteSpace(status.ObjectiveExplanation))
        {
            AnsiConsole.MarkupLine("[dim]There is no objective assessment to explain yet.[/]");
            return true;
        }

        AnsiConsole.MarkupLine("[bold]Detailed objective assessment[/]");
        AnsiConsole.WriteLine(status.ObjectiveExplanation);
        AnsiConsole.WriteLine();
        return true;
    }
}
