using Lattice.Client;
using Spectre.Console;

namespace Lattice.Repl;

internal sealed class ReplUtteranceProcessor(ILatticeSession session)
{
    public async ValueTask ProcessAsync(string input)
    {
        var result = await session.SubmitAsync(input);

        if (!result.IsSuccess)
        {
            AnsiConsole.Markup("[bold red]Lattice[/] [dim]›[/] ");
            AnsiConsole.WriteLine(result.Error ?? "Input could not be processed.");
            return;
        }

        foreach (var utterance in result.Utterances)
        {
            switch (utterance.Kind)
            {
                case LatticeUtteranceResultKind.StatementStored:
                    var entryKind = utterance.EntryKind?.ToString().ToLowerInvariant() ?? "statement";
                    AnsiConsole.MarkupLine($"[bold cyan]Lattice[/] [dim]› Stored {entryKind}.[/]");
                    AnsiConsole.Markup("[dim]meaning › [/]");
                    AnsiConsole.WriteLine(utterance.Meaning ?? string.Empty);
                    break;

                case LatticeUtteranceResultKind.StatementRejected:
                case LatticeUtteranceResultKind.UnsupportedQuestion:
                    AnsiConsole.Markup("[bold red]Lattice[/] [dim]›[/] ");
                    AnsiConsole.WriteLine(utterance.Message ?? "The utterance could not be processed.");
                    break;

                case LatticeUtteranceResultKind.ClarificationRequired:
                    AnsiConsole.Markup("[bold yellow]Lattice[/] [dim]›[/] ");
                    AnsiConsole.WriteLine(utterance.Message ?? "Please clarify the reference.");
                    break;

                case LatticeUtteranceResultKind.ObjectiveReceived:
                    AnsiConsole.Markup("[bold cyan]Lattice[/] [dim]›[/] ");
                    AnsiConsole.WriteLine(utterance.Message ?? "Objective retained.");
                    break;

                case LatticeUtteranceResultKind.ObjectiveInformationReceived:
                    AnsiConsole.Markup("[bold cyan]Lattice[/] [dim]›[/] ");
                    AnsiConsole.WriteLine(utterance.Message ?? "Objective information recorded.");
                    break;

                case LatticeUtteranceResultKind.QuestionAnswered:
                    WriteAnswer(utterance);
                    break;
            }
        }
    }

    private static void WriteAnswer(LatticeUtteranceResult utterance)
    {
        switch (utterance.TruthValue)
        {
            case LatticeTruthValue.True:
                AnsiConsole.MarkupLine("[bold green]Lattice[/] [dim]›[/] Yes.");
                break;
            case LatticeTruthValue.False:
                AnsiConsole.MarkupLine("[bold yellow]Lattice[/] [dim]›[/] No (explicit negative evidence).");
                break;
            case LatticeTruthValue.Both:
                AnsiConsole.MarkupLine("[bold red]Lattice[/] [dim]›[/] Contradiction: both the statement and its negation are supported.");
                break;
            default:
                AnsiConsole.MarkupLine("[bold yellow]Lattice[/] [dim]›[/] Unknown.");
                break;
        }

        ReplProofFormatter.WriteProofGroup(
            "Evidence supporting the answer:",
            utterance.SupportingEvidence);
        ReplProofFormatter.WriteProofGroup(
            "Evidence opposing the answer:",
            utterance.OpposingEvidence);
    }
}
