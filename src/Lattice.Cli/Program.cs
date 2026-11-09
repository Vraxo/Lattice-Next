using Lattice.Core.Context;
using Lattice.Core.Language;
using Lattice.Core.Intelligence;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using Lattice.Core.Tools;
using Lattice.Tooling.Stdio;
using Lattice.Translators.LmStudio;
using Spectre.Console;

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

var toolRegistry = new ToolRegistry(registeredTools, grantedPermissions);
var intentRecognizers = registeredTools
    .Where(tool => !tool.Descriptor.Intents.IsDefaultOrEmpty)
    .SelectMany(tool => tool.Descriptor.Intents.Select(
        definition => new ManifestToolIntentRecognizer(tool.Descriptor, definition)));
var toolIntentResolver = new ToolIntentResolver(intentRecognizers);

AnsiConsole.MarkupLine("[bold cyan]Lattice[/]");
AnsiConsole.MarkupLine("[dim]Symbolic intelligence · LCE 0.9[/]");
AnsiConsole.MarkupLine("[dim]Enter LCE sentences or use :translate for ordinary-language statements; use :load, :tools, :tool, :translator setup, :help, :status, or :quit.[/]");
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
        AnsiConsole.MarkupLine("[bold]Commands[/]");
        AnsiConsole.MarkupLine("  [green]:tools[/]  [dim]list registered capabilities[/]");
        AnsiConsole.MarkupLine("  [green]:translator[/]  [dim]show local LM Studio configuration[/]");
        AnsiConsole.MarkupLine("  [green]:translator setup[/]  [dim]start LM Studio, discover installed models, and save a selection[/]");
        AnsiConsole.MarkupLine("  [green]:translate <text>[/]  [dim]translate one ordinary-language statement/question into LCE for review[/]");
        AnsiConsole.MarkupLine("  [dim]Translation is opt-in. Lattice never stores a model proposal without your explicit selection.[/]");
        AnsiConsole.MarkupLine("  [green]What is 12 multiplied by 3?[/] [dim]natural-language calculator request[/]");
        AnsiConsole.MarkupLine("  [green]Convert 150 cm to meters.[/] [dim]natural-language unit conversion[/]");
        AnsiConsole.MarkupLine("  [green]Multiply that by 5, then subtract 10.[/] [dim]chains operations over successive results[/]");
        AnsiConsole.MarkupLine("  [green]:tool calculator {\"left\":12,\"operation\":\"multiply\",\"right\":3}[/] [dim]explicit diagnostic invocation[/]");
        AnsiConsole.MarkupLine("  [dim]Natural-language selection uses exact phrase patterns declared by discovered tool manifests.[/]");
        AnsiConsole.MarkupLine("  [dim]Patterns support single-token numbers and words, multiword text captures, and optional literal phrases; they are not unrestricted English.[/]");
        AnsiConsole.MarkupLine("  [dim]The bundled calculator and unit converter are Python tools; Python 3 must be available as 'python'.[/]");
        AnsiConsole.MarkupLine("  [dim]Tool results remain sourced observations; they are not added to session knowledge.[/]");
        AnsiConsole.MarkupLine("  [dim]Tools are discovered from ./tools or LATTICE_TOOLS_DIR and start only when invoked.[/]");
        AnsiConsole.MarkupLine("  [dim]LATTICE_TOOL_PERMISSIONS may grant read, write, or read,write to registered tools.[/]");
        AnsiConsole.MarkupLine("[bold]Supported sentences[/]");
        AnsiConsole.MarkupLine("  [green]Every dog is an animal.[/]  [dim]single-condition rule[/]");
        AnsiConsole.MarkupLine("  [green]Every red ball is a toy.[/] [dim]conjunctive class rule[/]");
        AnsiConsole.MarkupLine("  [green]A red ball exists.[/]     [dim]existential assertion[/]");
        AnsiConsole.MarkupLine("  [green]A red ball is shiny.[/]   [dim]indefinite property assertion[/]");
        AnsiConsole.MarkupLine("  [green]A red ball is a toy.[/]   [dim]existential class assertion[/]");
        AnsiConsole.MarkupLine("  [green]Does a red ball exist?[/] [dim]existence question[/]");
        AnsiConsole.MarkupLine("  [green]Fido is a dog.[/]            [dim]unary fact[/]");
        AnsiConsole.MarkupLine("  [green]It is a mammal.[/]          [dim]fact with reference[/]");
        AnsiConsole.MarkupLine("  [green]Fido likes milk.[/]         [dim]relation fact[/]");
        AnsiConsole.MarkupLine("  [green]It likes milk.[/]           [dim]relation fact with subject reference[/]");
        AnsiConsole.MarkupLine("  [green]Fido likes it.[/]           [dim]relation fact with object reference[/]");
        AnsiConsole.MarkupLine("  [green]Fido likes the red ball.[/] [dim]descriptive relation[/]");
        AnsiConsole.MarkupLine("  [green]The red ball is shiny.[/] [dim]property assertion[/]");
        AnsiConsole.MarkupLine("  [green]Is the red ball red?[/]    [dim]property question[/]");
        AnsiConsole.MarkupLine("  [green]Does Fido like the red ball?[/] [dim]descriptive relation query[/]");
        AnsiConsole.MarkupLine("  [green]Does Fido like milk?[/]     [dim]relation question[/]");
        AnsiConsole.MarkupLine("  [green]Does it like milk?[/]       [dim]relation question with subject reference[/]");
        AnsiConsole.MarkupLine("  [green]Does Fido like it?[/]       [dim]relation question with object reference[/]");
        AnsiConsole.MarkupLine("  [green]Is Fido an animal?[/]       [dim]unary question[/]");
        AnsiConsole.MarkupLine("  [green]Is it a mammal?[/]          [dim]question with reference[/]");
        AnsiConsole.MarkupLine("[dim]Relation questions use the verb's base form; the parser maps it to third-person singular.[/]");
        AnsiConsole.MarkupLine("[dim]Universal rules infer unary or fixed-object relational conclusions from one or more unary conditions.[/]");
        AnsiConsole.MarkupLine("[dim]Combine complete sentences in one input; they are processed in order.[/]");
        AnsiConsole.MarkupLine("[dim]The word 'it' resolves only when exactly one individual is known in this session.[/]");
        AnsiConsole.MarkupLine("[dim]When the reference is missing or ambiguous, Lattice asks you to name the individual.[/]");
        AnsiConsole.MarkupLine("[dim]Input cleanup handles formatting artifacts only; spelling and grammar are not corrected.[/]");
        AnsiConsole.MarkupLine("[dim]Unknown means no proof was found; it does not mean false.[/]");
        AnsiConsole.MarkupLine("[dim]Definite descriptions become canonical identifiers (for example, 'the red ball' becomes red-ball).[/]");
        AnsiConsole.MarkupLine("[dim]Definite descriptions contribute head-noun and modifier facts to the knowledge base.[/]");
        AnsiConsole.MarkupLine("[dim]A rule such as 'Every red ball is a toy' requires both red(x) and ball(x).[/]");
        AnsiConsole.MarkupLine("[dim]Indefinite descriptions introduce unnamed witnesses; separate statements are not assumed to describe the same individual.[/]");
        AnsiConsole.MarkupLine("[dim]Existence questions can be supported by one matching witness or one already-known named individual.[/]");
        AnsiConsole.MarkupLine("[dim]All conditions must be supported for the same individual before a rule can fire.[/]");
        AnsiConsole.MarkupLine("[dim]Definite descriptions do not resolve synonyms; indefinite relational arguments remain unsupported.[/]");
        AnsiConsole.MarkupLine("[dim]LCE 0.9 is a small controlled-language subset, not full ACE or unrestricted English.[/]");
        AnsiConsole.WriteLine();
        continue;
    }

    if (string.Equals(input, ":translator", StringComparison.OrdinalIgnoreCase))
    {
        AnsiConsole.MarkupLine("[bold]Local semantics translator[/]");
        AnsiConsole.MarkupLine($"  [cyan]Configuration[/]  {configurationStore.Path}");
        AnsiConsole.MarkupLine($"  [cyan]API[/]            {latticeConfiguration.LmStudio.ApiUrl}");
        AnsiConsole.MarkupLine($"  [cyan]LM Studio CLI[/]  {latticeConfiguration.LmStudio.LmsExecutable}");

        if (string.IsNullOrWhiteSpace(latticeConfiguration.LmStudio.ModelKey))
        {
            AnsiConsole.MarkupLine("  [yellow]No model selected[/]");
            AnsiConsole.MarkupLine("  Run :translator setup to discover and select an installed LLM.");
        }
        else
        {
            AnsiConsole.MarkupLine($"  [cyan]Model key[/]      {latticeConfiguration.LmStudio.ModelKey}");
            AnsiConsole.MarkupLine($"  [cyan]Model ID[/]       {latticeConfiguration.LmStudio.ModelIdentifier ?? "(assigned when loaded)"}");
            AnsiConsole.MarkupLine("  [dim]The server and model are checked and started automatically when you translate.[/]");
        }

        AnsiConsole.WriteLine();
        continue;
    }

    if (input.Equals(":translator setup", StringComparison.OrdinalIgnoreCase))
    {
        try
        {
            var models = await lmStudioRuntime.ListInstalledModelsAsync(
                ReportLmStudioStatus);

            if (models.IsEmpty)
            {
                AnsiConsole.MarkupLine("[yellow]No installed language models were found in LM Studio.[/]");
                AnsiConsole.MarkupLine("[dim]Download or import a GGUF model in LM Studio, then run :translator setup again.[/]");
                AnsiConsole.WriteLine();
                continue;
            }

            AnsiConsole.MarkupLine("[bold]Installed language models[/]");

            for (var index = 0; index < models.Length; index++)
            {
                var model = models[index];
                var parameters = string.IsNullOrWhiteSpace(model.Parameters)
                    ? string.Empty
                    : $" · {model.Parameters}";
                var loaded = model.LoadedInstanceIds.IsDefaultOrEmpty
                    ? string.Empty
                    : " · loaded";
                AnsiConsole.MarkupLine(
                    $"  [cyan]{index + 1}.[/] {model.DisplayName}{parameters}{loaded}");
                AnsiConsole.MarkupLine($"    [dim]{model.Key}[/]");
            }

            AnsiConsole.Markup("[yellow]Select a model number, or press Enter to cancel: [/]");
            var selection = Console.ReadLine();

            if (!int.TryParse(selection, out var selectedIndex) ||
                selectedIndex < 1 ||
                selectedIndex > models.Length)
            {
                AnsiConsole.MarkupLine("[dim]Setup cancelled; no configuration was changed.[/]");
                AnsiConsole.WriteLine();
                continue;
            }

            var selectedModel = models[selectedIndex - 1];
            var selectedConfiguration = latticeConfiguration.LmStudio with
            {
                ModelKey = selectedModel.Key,
                ModelIdentifier = selectedModel.LoadedInstanceIds.IsDefaultOrEmpty
                    ? null
                    : selectedModel.LoadedInstanceIds[0]
            };
            var updatedConfiguration = latticeConfiguration with
            {
                LmStudio = selectedConfiguration
            };

            await configurationStore.SaveAsync(updatedConfiguration);
            latticeConfiguration = updatedConfiguration;
            translationService = CreateTranslationService(selectedConfiguration);

            AnsiConsole.MarkupLine(
                $"[bold cyan]Lattice[/] [dim]› Saved '{selectedModel.DisplayName}' to lattice.config.json.[/]");
            AnsiConsole.MarkupLine("[dim]The server and selected model will be started/loaded automatically when :translate is used.[/]");
        }
        catch (Exception exception)
        {
            AnsiConsole.Markup("[bold red]Lattice[/] [dim]› LM Studio setup failed: [/]");
            AnsiConsole.WriteLine(exception.Message);
        }

        AnsiConsole.WriteLine();
        continue;
    }


    {
        AnsiConsole.MarkupLine("[bold]Local semantics translator[/]");

        if (lmStudioOptions is null)
        {
            AnsiConsole.MarkupLine("  [yellow]Disabled[/]");
            AnsiConsole.MarkupLine("  Set LATTICE_LMSTUDIO_MODEL to the exact model identifier exposed by LM Studio.");

            if (!string.IsNullOrWhiteSpace(lmStudioConfigurationError))
            {
                AnsiConsole.WriteLine(lmStudioConfigurationError);
            }
        }
        else
        {
            AnsiConsole.MarkupLine($"  [cyan]Provider[/]       LM Studio");
            AnsiConsole.MarkupLine($"  [cyan]Endpoint[/]       {lmStudioOptions.BaseAddress}");
            AnsiConsole.MarkupLine($"  [cyan]Model[/]          {lmStudioOptions.Model}");
            AnsiConsole.MarkupLine($"  [cyan]Auto-start[/]     {lmStudioOptions.AutoStartServer}");
            AnsiConsole.MarkupLine($"  [cyan]Auto-load[/]      {lmStudioOptions.AutoLoadModel}");
            AnsiConsole.MarkupLine("  [dim]Model proposals are parsed and validated, then require explicit acceptance.[/]");
        }

        AnsiConsole.WriteLine();
        continue;
    }

    if (input.Equals(":translate", StringComparison.OrdinalIgnoreCase) ||
        input.StartsWith(":translate ", StringComparison.OrdinalIgnoreCase))
    {
        var separator = input.IndexOf(' ');
        var sourceText = separator < 0 ? string.Empty : input[(separator + 1)..].Trim();

        if (sourceText.Length == 0)
        {
            AnsiConsole.MarkupLine("[bold yellow]Lattice[/] [dim]›[/] Usage: :translate <one statement or question>");
            AnsiConsole.WriteLine();
            continue;
        }

        if (translationService is null)
        {
            AnsiConsole.MarkupLine("[bold yellow]Lattice[/] [dim]›[/] Local translation is not configured. Run :translator setup first.");
            AnsiConsole.WriteLine();
            continue;
        }

        var translation = await translationService.TranslateAsync(sourceText);

        if (translation.Status == SemanticTranslationStatus.ReadyForReview)
        {
            var candidate = translation.Candidates[0];
            AnsiConsole.MarkupLine("[bold cyan]Lattice[/] [dim]› Proposed meaning (not yet accepted):[/]");
            AnsiConsole.WriteLine(SemanticFormatter.Format(candidate));
            AnsiConsole.Markup("[yellow]Accept and process this interpretation? [y/N] [/]");
            var acceptance = Console.ReadLine();

            if (string.Equals(acceptance?.Trim(), "y", StringComparison.OrdinalIgnoreCase))
            {
                ProcessUtterance(candidate);
            }
            else
            {
                AnsiConsole.MarkupLine("[dim]Candidate discarded; session knowledge was not changed.[/]");
            }

            AnsiConsole.WriteLine();
            continue;
        }

        if (translation.Status == SemanticTranslationStatus.Ambiguous)
        {
            AnsiConsole.MarkupLine("[bold yellow]Lattice[/] [dim]› Multiple interpretations were proposed:[/]");
            for (var index = 0; index < translation.Candidates.Length; index++)
            {
                AnsiConsole.MarkupLine(
                    $"  [cyan]{index + 1}.[/] {SemanticFormatter.Format(translation.Candidates[index])}");
            }

            AnsiConsole.Markup("[yellow]Choose a candidate number, or press Enter to cancel: [/]");
            var selection = Console.ReadLine();

            if (int.TryParse(selection, out var selectedIndex) &&
                selectedIndex >= 1 &&
                selectedIndex <= translation.Candidates.Length)
            {
                ProcessUtterance(translation.Candidates[selectedIndex - 1]);
            }
            else
            {
                AnsiConsole.MarkupLine("[dim]No candidate accepted; session knowledge was not changed.[/]");
            }

            AnsiConsole.WriteLine();
            continue;
        }

        AnsiConsole.Markup("[bold yellow]Lattice[/] [dim]›[/] ");
        AnsiConsole.WriteLine(translation.Message ?? translation.Status.ToString());
        AnsiConsole.WriteLine();
        continue;
    }

    if (input.StartsWith(":load ", StringComparison.OrdinalIgnoreCase))
    {
        var path = input[6..].Trim().Trim('"');

        if (path.Length == 0)
        {
            AnsiConsole.MarkupLine("[bold yellow]Lattice[/] [dim]›[/] Usage: :load <path-to-pack.json>");
            AnsiConsole.WriteLine();
            continue;
        }

        if (!File.Exists(path))
        {
            AnsiConsole.MarkupLine("[bold red]Lattice[/] [dim]›[/] File not found.");
            AnsiConsole.WriteLine();
            continue;
        }

        string packJson;

        try
        {
            packJson = File.ReadAllText(path);
        }
        catch (IOException)
        {
            AnsiConsole.MarkupLine("[bold red]Lattice[/] [dim]›[/] The pack file could not be read.");
            AnsiConsole.WriteLine();
            continue;
        }
        catch (UnauthorizedAccessException)
        {
            AnsiConsole.MarkupLine("[bold red]Lattice[/] [dim]›[/] Access to the pack file was denied.");
            AnsiConsole.WriteLine();
            continue;
        }

        if (!knowledgePackLoader.TryLoadAndAdd(packJson, knowledgeBase, out var pack, out var error))
        {
            AnsiConsole.Markup("[bold red]Lattice[/] [dim]›[/] ");
            AnsiConsole.WriteLine(error ?? "The knowledge pack could not be loaded.");
            AnsiConsole.WriteLine();
            continue;
        }

        loadedPacks.Add(pack!);
        AnsiConsole.MarkupLine($"[bold cyan]Lattice[/] [dim]› Loaded '{pack!.Name}' ({pack.ClaimCount} claims).[/]");
        AnsiConsole.WriteLine();
        continue;
    }

    if (string.Equals(input, ":tools", StringComparison.OrdinalIgnoreCase))
    {
        AnsiConsole.MarkupLine("[bold]Available tools[/]");

        foreach (var descriptor in toolRegistry.Descriptors.OrderBy(item => item.Id, StringComparer.Ordinal))
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
        var result = await toolRegistry.TryInvokeAsync(toolId, arguments);

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
        AnsiConsole.MarkupLine("  [cyan]Language[/]             LCE 0.9 parser");
        AnsiConsole.MarkupLine("  [cyan]Semantics[/]            Terms, n-ary predicates, conjunction, existential and universal quantification");
        AnsiConsole.MarkupLine("  [cyan]Reasoning[/]            Unary and fixed-object relational inference + existential lookup");
        AnsiConsole.MarkupLine($"  [cyan]Session knowledge[/]    {knowledgeBase.Facts.Count} facts, {knowledgeBase.Rules.Count} rules");
        AnsiConsole.MarkupLine($"  [cyan]Known individuals[/]    {conversationContext.KnownIndividualCount}");
        AnsiConsole.MarkupLine("  [cyan]Persistence[/]          [yellow]Current session only[/]");
        AnsiConsole.MarkupLine("  [cyan]Pack loading[/]         Version 1 JSON; imported claims retain pack provenance");
        AnsiConsole.MarkupLine("  [cyan]Provenance[/]            Structured sources; duplicate claims retain all sources");
        AnsiConsole.MarkupLine($"  [cyan]Knowledge packs[/]      {loadedPacks.Count} loaded");
        AnsiConsole.MarkupLine($"  [cyan]Tools[/]                {toolRegistry.Descriptors.Count} registered");
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
            var result = await toolRegistry.TryInvokeAsync(
                currentIntent.ToolId,
                currentIntent.ArgumentsJson);

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

            var descriptor = toolRegistry.Descriptors.FirstOrDefault(
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
        ProcessUtterance(parsedUtterance);
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
        ExistentialQuantification existentialQuery => reasoningEngine.Ask(knowledgeBase, existentialQuery),
        _ => null
    };

    if (answer is null)
    {
        AnsiConsole.MarkupLine("[bold red]Lattice[/] [dim]› Unsupported question form.[/]");
        return;
    }

    conversationContext.Observe(utterance);

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
    var emitted = new HashSet<string>(StringComparer.Ordinal);
    return Collect(proof, emitted);

    static IEnumerable<string> Collect(ProofNode current, HashSet<string> emitted)
    {
        if (current.Premise is not null)
        {
            foreach (var statement in Collect(current.Premise, emitted))
            {
                yield return statement;
            }
        }

        if (!current.AdditionalPremises.IsDefaultOrEmpty)
        {
            foreach (var additionalPremise in current.AdditionalPremises)
            {
                foreach (var statement in Collect(additionalPremise, emitted))
                {
                    yield return statement;
                }
            }
        }

        foreach (var source in current.Sources)
        {
            var referent = source.ResolvedReferent is { } resolved
                ? $" [it resolved to {resolved.Value}]"
                : string.Empty;
            var origin = source.Kind == KnowledgeSourceKind.UserAssertion
                ? string.Empty
                : $" [source: {source.Kind}]";
            var pack = string.IsNullOrWhiteSpace(source.SourceId)
                ? string.Empty
                : $" [pack: {source.SourceId}]";
            var statement = $"{source.SourceText}{referent}{origin}{pack}";

            if (emitted.Add(statement))
            {
                yield return statement;
            }
        }
    }
}
