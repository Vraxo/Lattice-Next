using Lattice.Client;
using Lattice.Core.Semantics;
using Spectre.Console;

namespace Lattice.Repl;

internal sealed class ReplUtteranceProcessor(ILatticeSession session)
{
    public ValueTask ProcessAsync(
        string input,
        CancellationToken cancellationToken = default) =>
        ProcessInputAsync(input, cancellationToken);

    public ValueTask ProcessAsync(
        SemanticUtterance parsedUtterance,
        CancellationToken cancellationToken = default) =>
        ProcessInputAsync(SemanticFormatter.Format(parsedUtterance), cancellationToken);

    private async ValueTask ProcessInputAsync(
        string input,
        CancellationToken cancellationToken)
    {
        var result = await session.SubmitAsync(input, cancellationToken);

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
                    AnsiConsole.MarkupLine(
                        $"[bold cyan]Lattice[/] [dim]› Stored {(utterance.EntryKind ?? LatticeKnowledgeEntryKind.Fact).ToString().ToLowerInvariant()}.[/]");
                    AnsiConsole.Markup("[dim]meaning › [/]");
                    AnsiConsole.WriteLine(utterance.Meaning ?? string.Empty);
                    break;

                case LatticeUtteranceResultKind.StatementRejected:
                case LatticeUtteranceResultKind.ClarificationRequired:
                case LatticeUtteranceResultKind.UnsupportedQuestion:
                    AnsiConsole.Markup("[bold yellow]Lattice[/] [dim]›[/] ");
                    AnsiConsole.WriteLine(utterance.Message ?? "The utterance could not be processed.");
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

        WriteEvidenceGroup("Evidence supporting the answer:", utterance.SupportingEvidence);
        WriteEvidenceGroup("Evidence opposing the answer:", utterance.OpposingEvidence);
    }

    private static void WriteEvidenceGroup(
        string heading,
        IReadOnlyList<LatticeEvidence> evidence)
    {
        if (evidence.Count == 0)
        {
            return;
        }

        AnsiConsole.MarkupLine($"[dim]{heading}[/]");

        foreach (var item in evidence)
        {
            foreach (var source in GetSources(item))
            {
                AnsiConsole.Markup("[dim]  • [/]");
                AnsiConsole.WriteLine(source);
            }
        }
    }

    private static IEnumerable<string> GetSources(LatticeEvidence evidence)
    {
        foreach (var premise in evidence.Premises)
        {
            foreach (var source in GetSources(premise))
            {
                yield return source;
            }
        }

        foreach (var source in evidence.Sources)
        {
            var referent = source.ResolvedReferent is null
                ? string.Empty
                : $" [it resolved to {source.ResolvedReferent}]";
            var origin = source.Kind == LatticeKnowledgeSourceKind.UserAssertion
                ? string.Empty
                : $" [source: {source.Kind}]";
            var pack = string.IsNullOrWhiteSpace(source.SourceId)
                ? string.Empty
                : $" [pack: {source.SourceId}]";
            yield return $"{source.SourceText}{referent}{origin}{pack}";
        }
    }
}
