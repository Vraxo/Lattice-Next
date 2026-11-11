using Lattice.Core.Context;
using Lattice.Core.Intelligence;
using Lattice.Core.Reasoning;
using Lattice.Core.Tools;
using Lattice.Translators.LmStudio;
using Spectre.Console;

namespace Lattice.Cli;

internal sealed class CliSessionCommands
{
    private const int MaximumToolIntentSteps = 8;

    private readonly CliToolAccess toolAccess;
    private readonly ConversationContext conversationContext;
    private readonly KnowledgeBase knowledgeBase;
    private readonly List<KnowledgePack> loadedPacks;
    private readonly ToolIntentResolver toolIntentResolver;
    private readonly LmStudioConfigurationStore configurationStore;
    private readonly bool configurationLoaded;
    private readonly CliTranslationCommands translationCommands;

    public CliSessionCommands(
        CliToolAccess toolAccess,
        ConversationContext conversationContext,
        KnowledgeBase knowledgeBase,
        List<KnowledgePack> loadedPacks,
        ToolIntentResolver toolIntentResolver,
        LmStudioConfigurationStore configurationStore,
        bool configurationLoaded,
        CliTranslationCommands translationCommands)
    {
        this.toolAccess = toolAccess;
        this.conversationContext = conversationContext;
        this.knowledgeBase = knowledgeBase;
        this.loadedPacks = loadedPacks;
        this.toolIntentResolver = toolIntentResolver;
        this.configurationStore = configurationStore;
        this.configurationLoaded = configurationLoaded;
        this.translationCommands = translationCommands;
    }

    public async ValueTask<bool> TryHandleAsync(string input)
    {
        if (string.Equals(input, ":permissions", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine("[bold]Tool permissions[/]");

            if (toolAccess.RememberedToolPermissions.Count == 0)
            {
                AnsiConsole.MarkupLine("  [dim]No remembered tool approvals.[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("  [cyan]Remembered[/]");

                foreach (var pair in toolAccess.RememberedToolPermissions.OrderBy(
                    item => item.Key,
                    StringComparer.OrdinalIgnoreCase))
                {
                    AnsiConsole.MarkupLine(
                        $"    {Markup.Escape(pair.Key)} — {Markup.Escape(CliToolAccess.FormatPermissionNames(pair.Value))}");
                }
            }

            if (toolAccess.SessionToolPermissions.Count > 0)
            {
                AnsiConsole.MarkupLine("  [cyan]This session only[/]");

                foreach (var pair in toolAccess.SessionToolPermissions.OrderBy(
                    item => item.Key,
                    StringComparer.OrdinalIgnoreCase))
                {
                    AnsiConsole.MarkupLine(
                        $"    {Markup.Escape(pair.Key)} — {Markup.Escape(CliToolAccess.FormatPermissionNames(pair.Value))}");
                }
            }

            if (toolAccess.GrantedPermissions != ToolPermission.None)
            {
                AnsiConsole.MarkupLine(
                    $"  [cyan]Environment pre-grants[/] {Markup.Escape(CliToolAccess.FormatPermissionNames(toolAccess.GrantedPermissions))}");
            }

            AnsiConsole.MarkupLine("  [dim]Use :permissions revoke <tool-id> to forget a remembered approval.[/]");
            AnsiConsole.WriteLine();
            return true;
        }

        if (input.StartsWith(":permissions revoke ", StringComparison.OrdinalIgnoreCase))
        {
            var toolId = input[20..].Trim();

            if (toolId.Length == 0)
            {
                AnsiConsole.MarkupLine("[yellow]Usage: :permissions revoke <tool-id>[/]");
                AnsiConsole.WriteLine();
                return true;
            }

            var removedRemembered = toolAccess.RememberedToolPermissions.Remove(toolId);
            var removedSession = toolAccess.SessionToolPermissions.Remove(toolId);

            if (!removedRemembered && !removedSession)
            {
                AnsiConsole.MarkupLine(
                    $"[yellow]No remembered or session-only grant exists for '{Markup.Escape(toolId)}'.[/]");
                AnsiConsole.WriteLine();
                return true;
            }

            var persistenceFailed = false;

            if (removedRemembered && configurationLoaded)
            {
                var updatedConfiguration = toolAccess.Configuration with
                {
                    ToolPermissions = new Dictionary<string, ToolPermission>(
                        toolAccess.RememberedToolPermissions,
                        StringComparer.OrdinalIgnoreCase)
                };

                try
                {
                    await configurationStore.SaveAsync(updatedConfiguration);
                    toolAccess.UpdateConfiguration(updatedConfiguration);
                    translationCommands.UpdateConfiguration(updatedConfiguration);
                }
                catch (IOException)
                {
                    persistenceFailed = true;
                }
                catch (UnauthorizedAccessException)
                {
                    persistenceFailed = true;
                }
            }

            toolAccess.RefreshRegistry();
            AnsiConsole.MarkupLine(
                $"[green]Revoked permissions for '{Markup.Escape(toolId)}' in this session.[/]");

            if (persistenceFailed)
            {
                AnsiConsole.MarkupLine(
                    "[yellow]The saved approval could not be removed from lattice.config.json; it may return next time.[/]");
            }
            else if (removedRemembered)
            {
                AnsiConsole.MarkupLine("[dim]The remembered approval was removed from lattice.config.json.[/]");
            }

            AnsiConsole.WriteLine();
            return true;
        }

        if (string.Equals(input, ":tools", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine("[bold]Available tools[/]");

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
