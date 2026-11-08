using Lattice.Core.Context;
using Lattice.Core.Language;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using Spectre.Console;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var parser = new LceParser();
var knowledgeBase = new KnowledgeBase();
var conversationContext = new ConversationContext();
var reasoningEngine = new ReasoningEngine();

AnsiConsole.MarkupLine("[bold cyan]Lattice[/]");
AnsiConsole.MarkupLine("[dim]Symbolic intelligence · LCE 0.5[/]");
AnsiConsole.MarkupLine("[dim]Enter one or more sentences, :help for examples, :status for implementation status, or :quit to exit.[/]");
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
        AnsiConsole.MarkupLine("[bold]Supported sentences[/]");
        AnsiConsole.MarkupLine("  [green]Every dog is an animal.[/]  [dim]rule[/]");
        AnsiConsole.MarkupLine("  [green]Fido is a dog.[/]            [dim]unary fact[/]");
        AnsiConsole.MarkupLine("  [green]It is a mammal.[/]          [dim]fact with reference[/]");
        AnsiConsole.MarkupLine("  [green]Fido likes milk.[/]         [dim]relation fact[/]");
        AnsiConsole.MarkupLine("  [green]Fido likes the red ball.[/] [dim]descriptive noun phrase[/]");
        AnsiConsole.MarkupLine("  [green]Does Fido like the red ball?[/] [dim]descriptive relation query[/]");
        AnsiConsole.MarkupLine("  [green]Does Fido like milk?[/]     [dim]relation question[/]");
        AnsiConsole.MarkupLine("  [green]Is Fido an animal?[/]       [dim]unary question[/]");
        AnsiConsole.MarkupLine("  [green]Is it a mammal?[/]          [dim]question with reference[/]");
        AnsiConsole.MarkupLine("[dim]Relation questions use the verb's base form; the parser maps it to third-person singular.[/]");
        AnsiConsole.MarkupLine("[dim]Relational questions check exact stored facts; rules currently infer unary predicates only.[/]");
        AnsiConsole.MarkupLine("[dim]Combine complete sentences in one input; they are processed in order.[/]");
        AnsiConsole.MarkupLine("[dim]The word 'it' resolves only when exactly one individual is known in this session.[/]");
        AnsiConsole.MarkupLine("[dim]When the reference is missing or ambiguous, Lattice asks you to name the individual.[/]");
        AnsiConsole.MarkupLine("[dim]Input cleanup handles formatting artifacts only; spelling and grammar are not corrected.[/]");
        AnsiConsole.MarkupLine("[dim]Unknown means no proof was found; it does not mean false.[/]");
        AnsiConsole.MarkupLine("[dim]Definite descriptions become canonical identifiers (for example, 'the red ball' becomes red-ball).[/]");
        AnsiConsole.MarkupLine("[dim]Descriptions do not yet support adjective inference, indefinite phrases, or automatic co-reference.[/]");
        AnsiConsole.MarkupLine("[dim]LCE 0.5 is a small controlled-language subset, not full ACE or unrestricted English.[/]");
        AnsiConsole.WriteLine();
        continue;
    }

    if (string.Equals(input, ":status", StringComparison.OrdinalIgnoreCase))
    {
        AnsiConsole.MarkupLine("[bold]Implementation status[/]");
        AnsiConsole.MarkupLine("  [cyan]Language[/]             LCE 0.5 parser");
        AnsiConsole.MarkupLine("  [cyan]Semantics[/]            Terms, n-ary predicates, implication, universal quantification");
        AnsiConsole.MarkupLine("  [cyan]Reasoning[/]            Unary-rule chaining + direct relational lookup");
        AnsiConsole.MarkupLine($"  [cyan]Session knowledge[/]    {knowledgeBase.Facts.Count} facts, {knowledgeBase.Rules.Count} rules");
        AnsiConsole.MarkupLine($"  [cyan]Known individuals[/]    {conversationContext.KnownIndividualCount}");
        AnsiConsole.MarkupLine("  [cyan]Persistence[/]          [yellow]Current session only[/]");
        AnsiConsole.MarkupLine("  [cyan]Knowledge packs[/]      [yellow]Not implemented[/]");
        AnsiConsole.MarkupLine("  [cyan]Tools[/]                None registered");
        AnsiConsole.WriteLine();
        continue;
    }

    var parseResult = parser.ParseText(input);

    if (!parseResult.IsSuccess)
    {
        AnsiConsole.Markup("[bold red]Lattice[/] [dim]›[/] ");
        AnsiConsole.WriteLine(parseResult.Error ?? "Input could not be parsed.");
        AnsiConsole.WriteLine();
        continue;
    }

    foreach (var parsedUtterance in parseResult.Utterances)
    {
        ProcessUtterance(parsedUtterance);
    }

    AnsiConsole.WriteLine();
}

void ProcessUtterance(SemanticUtterance parsedUtterance)
{
    var resolution = conversationContext.Resolve(parsedUtterance);

    if (!resolution.IsSuccess)
    {
        AnsiConsole.Markup("[bold yellow]Lattice[/] [dim]›[/] ");

        var clarification = resolution.Status switch
        {
            ReferenceResolutionStatus.MissingReferent =>
                "I don't know who 'it' refers to yet. Please name the individual.",
            ReferenceResolutionStatus.AmbiguousReferent =>
                "More than one individual is known. Which one do you mean by 'it'? Please name them.",
            _ => "I couldn't resolve that reference. Please name the individual."
        };

        AnsiConsole.WriteLine(clarification);
        return;
    }

    var utterance = resolution.Utterance!;

    if (utterance.Kind == UtteranceKind.Assertion)
    {
        if (!knowledgeBase.TryAdd(utterance, out var kind, out var error))
        {
            AnsiConsole.Markup("[bold red]Lattice[/] [dim]›[/] ");
            AnsiConsole.WriteLine(error ?? "The statement could not be stored.");
            return;
        }

        conversationContext.Observe(utterance);

        AnsiConsole.MarkupLine(
            $"[bold cyan]Lattice[/] [dim]› Stored {kind.ToString().ToLowerInvariant()}.[/]");
        AnsiConsole.Markup("[dim]meaning › [/]");
        AnsiConsole.WriteLine(SemanticFormatter.Format(utterance));
        return;
    }

    if (utterance.Meaning is not PredicateApplication query)
    {
        AnsiConsole.MarkupLine("[bold red]Lattice[/] [dim]› Unsupported question form.[/]");
        return;
    }

    conversationContext.Observe(utterance);
    var answer = reasoningEngine.Ask(knowledgeBase, query);

    if (!answer.IsSupported)
    {
        AnsiConsole.MarkupLine("[bold yellow]Lattice[/] [dim]›[/] Unknown.");
        AnsiConsole.MarkupLine("[dim]No supporting fact or rule is known in this session.[/]");
        return;
    }

    AnsiConsole.MarkupLine("[bold cyan]Lattice[/] [dim]›[/] Yes.");
    AnsiConsole.MarkupLine("[dim]Because:[/]");

    foreach (var statement in GetProofStatements(answer.Proof!))
    {
        AnsiConsole.Markup("[dim]  • [/]");
        AnsiConsole.WriteLine(statement);
    }
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

    yield return proof.ResolvedReferent is { } referent
        ? $"{proof.SourceText} [it resolved to {referent.Value}]"
        : proof.SourceText;
}
