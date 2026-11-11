using Lattice.Core.Context;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using Spectre.Console;

namespace Lattice.Cli;

internal sealed class CliUtteranceProcessor(
    ConversationContext conversationContext,
    KnowledgeBase knowledgeBase,
    ReasoningEngine reasoningEngine)
{
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
    
        var answer = utterance.Meaning switch
        {
            PredicateApplication predicateQuery => reasoningEngine.Ask(knowledgeBase, predicateQuery),
            Negation negatedQuery => reasoningEngine.Ask(knowledgeBase, negatedQuery),
            ExistentialQuantification existentialQuery => reasoningEngine.Ask(knowledgeBase, existentialQuery),
            UniversalQuantification universalQuery => reasoningEngine.Ask(knowledgeBase, universalQuery),
            _ => null
        };
    
        if (answer is null)
        {
            AnsiConsole.MarkupLine("[bold red]Lattice[/] [dim]› Unsupported question form.[/]");
            return;
        }
    
        conversationContext.Observe(utterance);
    
        switch (answer.TruthValue)
        {
            case TruthValue.True:
                AnsiConsole.MarkupLine("[bold green]Lattice[/] [dim]›[/] Yes.");
                break;
            case TruthValue.False:
                AnsiConsole.MarkupLine("[bold yellow]Lattice[/] [dim]›[/] No (explicit negative evidence).");
                break;
            case TruthValue.Both:
                AnsiConsole.MarkupLine("[bold red]Lattice[/] [dim]›[/] Contradiction: both the statement and its negation are supported.");
                break;
            default:
                AnsiConsole.MarkupLine("[bold yellow]Lattice[/] [dim]›[/] Unknown.");
                break;
        }
    
        CliProofFormatter.WriteProofGroup("Evidence supporting the answer:", answer.Proofs);
        CliProofFormatter.WriteProofGroup("Evidence opposing the answer:", answer.ContradictingProofs);
    }
}
