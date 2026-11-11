using Lattice.Core.Context;
using Lattice.Core.Language;
using Lattice.Core.Intelligence;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using Lattice.Core.Tools;
using Lattice.Tooling.Stdio;
using Lattice.Translators.LmStudio;
using Spectre.Console;
using Lattice.Cli;

Console.OutputEncoding = System.Text.Encoding.UTF8;

const int maximumToolIntentSteps = 8;
var parser = new LceParser();
var configurationStore = new LmStudioConfigurationStore();
var configurationLoaded = configurationStore.TryLoad(
    out var latticeConfiguration,
    out var configurationError);

if (!configurationLoaded)
{
    latticeConfiguration = new LatticeConfiguration();
}
using var lmStudioHttpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
var lmStudioRuntime = new LmStudioRuntime(
    lmStudioHttpClient,
    latticeConfiguration.LmStudio);
var translationService = CreateTranslationService(latticeConfiguration.LmStudio);
var knowledgeBase = new KnowledgeBase();
var conversationContext = new ConversationContext();
var reasoningEngine = new ReasoningEngine();
var utteranceProcessor = new CliUtteranceProcessor(
    conversationContext,
    knowledgeBase,
    reasoningEngine);
var knowledgePackLoader = new KnowledgePackLoader();
var loadedPacks = new List<KnowledgePack>();
var configuredToolsDirectory = Environment.GetEnvironmentVariable("LATTICE_TOOLS_DIR");
var toolsDirectory = string.IsNullOrWhiteSpace(configuredToolsDirectory)
    ? Path.Combine(Environment.CurrentDirectory, "tools")
    : configuredToolsDirectory;
var externalToolsDiscovered = StdioToolProvider.TryDiscover(
    toolsDirectory,
    out var discoveredTools,
    out var toolDiscoveryError);
var registeredTools = externalToolsDiscovered
    ? discoveredTools
    : Array.Empty<ITool>();
var grantedPermissions = ToolPermission.None;
var configuredPermissions = Environment.GetEnvironmentVariable("LATTICE_TOOL_PERMISSIONS");

if (!string.IsNullOrWhiteSpace(configuredPermissions))
{
    var knownPermissions = ToolPermission.Read | ToolPermission.Write;

    if (Enum.TryParse<ToolPermission>(configuredPermissions, true, out var parsedPermissions) &&
        (parsedPermissions & ~knownPermissions) == 0)
    {
        grantedPermissions = parsedPermissions;
    }
    else
    {
        Console.WriteLine("LATTICE_TOOL_PERMISSIONS is invalid; no external tool permissions were granted.");
    }
}

var toolAccess = new CliToolAccess(
    registeredTools,
    grantedPermissions,
    configurationStore,
    configurationLoaded,
    latticeConfiguration);
var intentRecognizers = registeredTools
    .Where(tool => !tool.Descriptor.Intents.IsDefaultOrEmpty)
    .SelectMany(tool => tool.Descriptor.Intents.Select(
        definition => new ManifestToolIntentRecognizer(tool.Descriptor, definition)));
var toolIntentResolver = new ToolIntentResolver(intentRecognizers);

var translationCommands = new CliTranslationCommands(
    latticeConfiguration,
    translationService,
    configurationStore,
    lmStudioRuntime,
    CreateTranslationService,
    ReportLmStudioStatus,
    utteranceProcessor);

AnsiConsole.MarkupLine("[bold cyan]Lattice[/]");
AnsiConsole.MarkupLine("[dim]Symbolic intelligence · LCE 0.9[/]");
AnsiConsole.MarkupLine("[dim]Enter LCE or supported English sentences directly; use :translate for model-assisted interpretation beyond native coverage.[/]");
AnsiConsole.MarkupLine($"[dim]Local configuration: {configurationStore.Path}[/]");

if (!configurationLoaded)
{
    AnsiConsole.MarkupLine("[yellow]LM Studio configuration could not be loaded; using defaults until setup is saved.[/]");
    AnsiConsole.WriteLine(configurationError ?? "The local configuration is invalid.");
}

if (translationService is null)
{
    AnsiConsole.MarkupLine("[dim]Local translator not configured. Run :translator setup to choose an installed model.[/]");
}
else
{
    AnsiConsole.MarkupLine($"[dim]Local translator configured: {latticeConfiguration.LmStudio.ModelKey}.[/]");
}
if (!externalToolsDiscovered)
{
    AnsiConsole.MarkupLine("[yellow]External tools could not be discovered; no external capabilities were loaded.[/]");
    AnsiConsole.WriteLine(toolDiscoveryError ?? "Tool discovery failed.");
}
else if (registeredTools.Count > 0)
{
    AnsiConsole.MarkupLine($"[dim]Discovered {registeredTools.Count} external tool(s).[/]");
}
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
        CliHelp.Write();
        continue;
    }

    if (await translationCommands.TryHandleAsync(input))
    {
        latticeConfiguration = translationCommands.Configuration;
        translationService = translationCommands.TranslationService;
        toolAccess.UpdateConfiguration(latticeConfiguration);
        continue;
    }

    if (CliKnowledgePackCommand.TryHandle(
        input,
        knowledgePackLoader,
        knowledgeBase,
        loadedPacks))
    {
        continue;
    }

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
        continue;
    }

    if (input.StartsWith(":permissions revoke ", StringComparison.OrdinalIgnoreCase))
    {
        var toolId = input[20..].Trim();

        if (toolId.Length == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Usage: :permissions revoke <tool-id>[/]");
            AnsiConsole.WriteLine();
            continue;
        }

        var removedRemembered = toolAccess.RememberedToolPermissions.Remove(toolId);
        var removedSession = toolAccess.SessionToolPermissions.Remove(toolId);

        if (!removedRemembered && !removedSession)
        {
            AnsiConsole.MarkupLine(
                $"[yellow]No remembered or session-only grant exists for '{Markup.Escape(toolId)}'.[/]");
            AnsiConsole.WriteLine();
            continue;
        }

        var persistenceFailed = false;

        if (removedRemembered && configurationLoaded)
        {
            var updatedConfiguration = latticeConfiguration with
            {
                ToolPermissions = new Dictionary<string, ToolPermission>(
                    toolAccess.RememberedToolPermissions,
                    StringComparer.OrdinalIgnoreCase)
            };

            try
            {
                await configurationStore.SaveAsync(updatedConfiguration);
                latticeConfiguration = updatedConfiguration;
                toolAccess.UpdateConfiguration(latticeConfiguration);
                translationCommands.UpdateConfiguration(latticeConfiguration);
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
        continue;
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
        continue;
    }

    if (input.StartsWith(":tool ", StringComparison.OrdinalIgnoreCase))
    {
        var invocation = input[6..].Trim();
        var separator = invocation.IndexOf(' ');

        if (separator <= 0 || separator == invocation.Length - 1)
        {
            AnsiConsole.MarkupLine("[bold yellow]Lattice[/] [dim]›[/] Usage: :tool <tool-id> <arguments-json>");
            AnsiConsole.WriteLine();
            continue;
        }

        var toolId = invocation[..separator];
        var arguments = invocation[(separator + 1)..].Trim();
        var result = await toolAccess.InvokeAsync(toolId, arguments);
        latticeConfiguration = toolAccess.Configuration;
        translationCommands.UpdateConfiguration(latticeConfiguration);

        if (result is null)
        {
            AnsiConsole.WriteLine();
            continue;
        }

        if (!result.IsSuccess)
        {
            AnsiConsole.Markup("[bold red]Lattice[/] [dim]›[/] ");
            AnsiConsole.WriteLine(result.Error ?? "The tool invocation failed.");
            AnsiConsole.WriteLine();
            continue;
        }

        AnsiConsole.MarkupLine($"[bold cyan]Lattice[/] [dim]› Observation from {result.Observation!.ToolId}:[/]");
        AnsiConsole.WriteLine(result.Observation.Content);
        conversationContext.Observe(result.Observation!);
        AnsiConsole.MarkupLine("[dim]This result is an observation; it has not been added to session knowledge.[/]");
        AnsiConsole.WriteLine();
        continue;
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
        AnsiConsole.MarkupLine($"  [cyan]Translation[/]          {(translationService is null ? "disabled" : $"LM Studio · {latticeConfiguration.LmStudio.ModelKey}")}");
        AnsiConsole.WriteLine();
        continue;
    }

    var intentResolution = toolIntentResolver.Resolve(
        input,
        conversationContext.LatestToolObservation);

    if (intentResolution.Status == ToolIntentResolutionStatus.Ambiguous)
    {
        AnsiConsole.MarkupLine(
            "[bold yellow]Lattice[/] [dim]›[/] I found more than one possible capability for that request. Please rephrase it.");
        AnsiConsole.WriteLine();
        continue;
    }

    if (intentResolution.IsResolved)
    {
        ToolIntent? currentIntent = intentResolution.Intent!;
        var responses = new List<string>();
        var usedTools = new List<string>();
        var invocationFailed = false;
        var invocationCount = 0;

        while (currentIntent is not null && invocationCount < maximumToolIntentSteps)
        {
            var result = await toolAccess.InvokeAsync(
                currentIntent.ToolId,
                currentIntent.ArgumentsJson);
            latticeConfiguration = toolAccess.Configuration;
            translationCommands.UpdateConfiguration(latticeConfiguration);

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
        utteranceProcessor.Process(parsedUtterance);
    }

    AnsiConsole.WriteLine();
}

SemanticTranslationService? CreateTranslationService(LmStudioConfiguration configuration) =>
    string.IsNullOrWhiteSpace(configuration.ModelKey)
        ? null
        : new SemanticTranslationService(
            new LmStudioSemanticsTranslator(
                lmStudioHttpClient,
                configuration,
                lmStudioRuntime,
                parser,
                ReportLmStudioStatus));

void ReportLmStudioStatus(string message) =>
    AnsiConsole.MarkupLine($"[dim]{Markup.Escape(message)}[/]");
