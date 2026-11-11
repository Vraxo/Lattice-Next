using Lattice.Core.Context;
using Lattice.Core.Intelligence;
using Spectre.Console;

namespace Lattice.Cli;

internal sealed class CliToolIntentCommand(
    CliToolAccess toolAccess,
    ConversationContext conversationContext,
    ToolIntentResolver toolIntentResolver,
    CliTranslationCommands translationCommands)
{
    private const int MaximumToolIntentSteps = 8;

    public async ValueTask<bool> TryHandleAsync(string input)
    {

        var intentResolution = toolIntentResolver.Resolve(
                    input,
                    conversationContext.LatestToolObservation);

                if (intentResolution.Status == ToolIntentResolutionStatus.Ambiguous)
                {
                    AnsiConsole.MarkupLine(
                        "[bold yellow]Lattice[/] [dim]›[/] I found more than one possible capability for that request. Please rephrase it.");
                    AnsiConsole.WriteLine();
                    return true;
                }

                if (intentResolution.IsResolved)
                {
                    ToolIntent? currentIntent = intentResolution.Intent!;
                    var responses = new List<string>();
                    var usedTools = new List<string>();
                    var invocationFailed = false;
                    var invocationCount = 0;

                    while (currentIntent is not null && invocationCount < MaximumToolIntentSteps)
                    {
                        var result = await toolAccess.InvokeAsync(
                            currentIntent.ToolId,
                            currentIntent.ArgumentsJson);
                        translationCommands.UpdateConfiguration(toolAccess.Configuration);

                        if (result is null)
                        {
                            invocationFailed = true;
                            break;
                        }

                        if (!result.IsSuccess)
                        {
                            AnsiConsole.Markup("[bold red]Lattice[/] [dim]›[/] ");
                            AnsiConsole.WriteLine(result.Error ?? "The capability could not complete the request.");
                            invocationFailed = true;
                            break;
                        }

                        invocationCount++;
                        var observation = result.Observation!;
                        conversationContext.Observe(observation);
                        responses.Add(currentIntent.FormatResponse(observation));

                        var descriptor = toolAccess.Registry.Descriptors.FirstOrDefault(
                            item => string.Equals(item.Id, currentIntent.ToolId, StringComparison.OrdinalIgnoreCase));
                        usedTools.Add(descriptor?.Name ?? currentIntent.ToolId);

                        var continuation = currentIntent.Continue;
                        currentIntent = continuation?.Invoke(observation);

                        if (continuation is not null && currentIntent is null)
                        {
                            AnsiConsole.Markup("[bold red]Lattice[/] [dim]›[/] ");
                            AnsiConsole.WriteLine(
                                "The capability chain could not continue because the preceding result was not numeric.");
                            invocationFailed = true;
                            break;
                        }
                    }

                    if (!invocationFailed && currentIntent is not null)
                    {
                        AnsiConsole.Markup("[bold red]Lattice[/] [dim]›[/] ");
                        AnsiConsole.WriteLine("The capability chain exceeded the maximum of 8 steps.");
                        invocationFailed = true;
                    }

                    if (!invocationFailed)
                    {
                        AnsiConsole.Markup("[bold cyan]Lattice[/] [dim]›[/] ");
                        AnsiConsole.WriteLine(string.Join(" Then ", responses));
                        AnsiConsole.MarkupLine(
                            $"[dim]Used {string.Join(", ", usedTools.Distinct(StringComparer.OrdinalIgnoreCase))}; results retained as temporary observations.[/]");
                    }

                    AnsiConsole.WriteLine();
                    return true;
                }

        return false;
    }
}
