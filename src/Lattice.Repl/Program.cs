using Lattice.Core.Context;
using Lattice.Core.Language;
using Lattice.Core.Intelligence;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using Lattice.Core.Tools;
using Lattice.Tooling.Stdio;
using Lattice.Translators.LmStudio;
using Spectre.Console;
using Lattice.Repl;

Console.OutputEncoding = System.Text.Encoding.UTF8;

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
var utteranceProcessor = new ReplUtteranceProcessor(
    conversationContext,
    knowledgeBase,
    reasoningEngine);
var knowledgePackLoader = new KnowledgePackLoader();
var loadedPacks = new List<KnowledgePack>();
var configuredToolsDirectory = Environment.GetEnvironmentVariable("LATTICE_TOOLS_DIR");
var toolsDirectory = ResolveToolsDirectory(configuredToolsDirectory);
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

var toolAccess = new ReplToolAccess(
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

var translationCommands = new ReplTranslationCommands(
    latticeConfiguration,
    translationService,
    configurationStore,
    lmStudioRuntime,
    CreateTranslationService,
    ReportLmStudioStatus,
    utteranceProcessor);

var sessionCommands = new ReplSessionCommands(
    toolAccess,
    conversationContext,
    knowledgeBase,
    loadedPacks,
    toolIntentResolver,
    configurationStore,
    configurationLoaded,
    translationCommands);

AnsiConsole.MarkupLine("[bold cyan]Lattice[/]");
AnsiConsole.MarkupLine("[dim]Symbolic intelligence · LCE 0.9[/]");
AnsiConsole.MarkupLine("[dim]Enter LCE or supported English sentences directly; use :translate for model-assisted interpretation beyond native coverage.[/]");
AnsiConsole.MarkupLine($"[dim]Local configuration: {configurationStore.Path}[/]");
AnsiConsole.MarkupLine($"[dim]External tools directory: {Markup.Escape(toolsDirectory)}[/]");

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
else
{
    AnsiConsole.MarkupLine(
        $"[yellow]No external tools were discovered in '{Markup.Escape(toolsDirectory)}'. Check that tool folders and their tool.json manifests are present.[/]");
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
        ReplHelp.Write();
        continue;
    }

    if (await translationCommands.TryHandleAsync(input))
    {
        latticeConfiguration = translationCommands.Configuration;
        translationService = translationCommands.TranslationService;
        toolAccess.UpdateConfiguration(latticeConfiguration);
        continue;
    }

    if (ReplKnowledgePackCommand.TryHandle(
        input,
        knowledgePackLoader,
        knowledgeBase,
        loadedPacks))
    {
        continue;
    }

    if (await sessionCommands.TryHandleAsync(input))
    {
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

string ResolveToolsDirectory(string? configuredDirectory)
{
    if (!string.IsNullOrWhiteSpace(configuredDirectory))
    {
        return Path.GetFullPath(configuredDirectory);
    }

    var workingDirectoryTools = Path.Combine(Environment.CurrentDirectory, "tools");

    if (Directory.Exists(workingDirectoryTools))
    {
        return Path.GetFullPath(workingDirectoryTools);
    }

    for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
        directory is not null;
        directory = directory.Parent)
    {
        var candidate = Path.Combine(directory.FullName, "tools");

        if (Directory.Exists(candidate))
        {
            return candidate;
        }
    }

    return Path.GetFullPath(workingDirectoryTools);
}
