using Lattice.Core.Language;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using Spectre.Console;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var parser = new LceParser();
var knowledgeBase = new KnowledgeBase();
var reasoningEngine = new ReasoningEngine();

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
        AnsiConsole.MarkupLine("  [green]Every dog is an animal.[/]  [dim]rule[/]");
        AnsiConsole.MarkupLine("  [green]Fido is a dog.[/]            [dim]fact[/]");
        AnsiConsole.MarkupLine("  [green]Is Fido an animal?[/]       [dim]question[/]");
        AnsiConsole.MarkupLine("[dim]Facts and rules remain available until this session ends.[/]");
        AnsiConsole.MarkupLine("[dim]Unknown means no proof was found; it does not mean false.[/]");
        AnsiConsole.MarkupLine("[dim]LCE 0.1 is a small controlled-language subset, not full ACE or unrestricted English.[/]");
        AnsiConsole.WriteLine();
        continue;
    }

    if (string.Equals(input, ":status", StringComparison.OrdinalIgnoreCase))
    {
        AnsiConsole.MarkupLine("[bold]Implementation status[/]");
        AnsiConsole.MarkupLine("  [cyan]Language[/]             LCE 0.1 parser");
        AnsiConsole.MarkupLine("  [cyan]Semantics[/]            Terms, predicates, implication, universal quantification");
        AnsiConsole.MarkupLine("  [cyan]Reasoning[/]            Unary facts and universal-rule forward chaining");
        AnsiConsole.MarkupLine($"  [cyan]Session knowledge[/]    {knowledgeBase.Facts.Count} facts, {knowledgeBase.Rules.Count} rules");
        AnsiConsole.MarkupLine("  [cyan]Persistence[/]          [yellow]Current session only[/]");
        AnsiConsole.MarkupLine("  [cyan]Knowledge packs[/]      [yellow]Not implemented[/]");
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

    if (utterance.Kind == UtteranceKind.Assertion)
    {
        if (!knowledgeBase.TryAdd(utterance, out var kind, out var error))
        {
            AnsiConsole.Markup("[bold red]Lattice[/] [dim]›[/] ");
            AnsiConsole.WriteLine(error ?? "The statement could not be stored.");
            AnsiConsole.WriteLine();
            continue;
        }

        AnsiConsole.MarkupLine(
            $"[bold cyan]Lattice[/] [dim]› Stored {kind.ToString().ToLowerInvariant()}.[/]");
        AnsiConsole.Markup("[dim]meaning › [/]");
        AnsiConsole.WriteLine(SemanticFormatter.Format(utterance));
        AnsiConsole.WriteLine();
        continue;
    }

    if (utterance.Meaning is not PredicateApplication query)
    {
        AnsiConsole.MarkupLine("[bold red]Lattice[/] [dim]› Unsupported question form.[/]");
        AnsiConsole.WriteLine();
        continue;
    }

    var answer = reasoningEngine.Ask(knowledgeBase, query);

    if (!answer.IsSupported)
    {
        AnsiConsole.MarkupLine("[bold yellow]Lattice[/] [dim]›[/] Unknown.");
        AnsiConsole.MarkupLine("[dim]No supporting fact or rule is known in this session.[/]");
    }
    else
    {
        AnsiConsole.MarkupLine("[bold cyan]Lattice[/] [dim]›[/] Yes.");
        AnsiConsole.MarkupLine("[dim]Because:[/]");

        foreach (var statement in GetProofStatements(answer.Proof!))
        {
            AnsiConsole.Markup("[dim]  • [/]");
            AnsiConsole.WriteLine(statement);
        }
    }

    AnsiConsole.WriteLine();
}

static IEnumerable<string> GetProofStatements(ProofNode proof)
{
    if (proof.Premise is not null)
    {
        foreach (var statement in GetProofStatements(proof.Premise))
        {
            yield return statement;
        }
    }

    yield return proof.SourceText;
}
