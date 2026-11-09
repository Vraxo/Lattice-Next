using Lattice.Core.Context;
using Lattice.Core.Language;
using Lattice.Core.Intelligence;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using Lattice.Core.Tools;
using Lattice.Tooling.Stdio;
using Spectre.Console;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var parser = new LceParser();
var knowledgeBase = new KnowledgeBase();
var conversationContext = new ConversationContext();
var reasoningEngine = new ReasoningEngine();
var knowledgePackLoader = new KnowledgePackLoader();
var loadedPacks = new List<KnowledgePack>();
var configuredToolsDirectory = Environment.GetEnvironmentVariable("LATTICE_TOOLS_DIR");
var toolsDirectory = string.IsNullOrWhiteSpace(configuredToolsDirectory)
    ? Path.Combine(Environment.CurrentDirectory, "tools")
    : configuredToolsDirectory;
var registeredTools = new List<ITool> { new CalculatorTool(), new UnitConverterTool() };
var externalToolsDiscovered = StdioToolProvider.TryDiscover(
    toolsDirectory,
    out var externalTools,
    out var toolDiscoveryError);
var registeredToolIds = new HashSet<string>(
    registeredTools.Select(tool => tool.Descriptor.Id),
    StringComparer.OrdinalIgnoreCase);
var externalToolCount = 0;

if (externalToolsDiscovered)
{
    foreach (var externalTool in externalTools)
    {
        if (!registeredToolIds.Add(externalTool.Descriptor.Id))
        {
            continue;
        }

        registeredTools.Add(externalTool);
        externalToolCount++;
    }
}

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
var toolIntentResolver = new ToolIntentResolver(
[
    new CalculatorIntentRecognizer(),
    new UnitConversionIntentRecognizer()
]);

AnsiConsole.MarkupLine("[bold cyan]Lattice[/]");
AnsiConsole.MarkupLine("[dim]Symbolic intelligence · LCE 0.8[/]");
AnsiConsole.MarkupLine("[dim]Enter ordinary-language calculations/conversions or LCE sentences; use :load, :tools, :tool, :help, :status, or :quit.[/]");
if (!externalToolsDiscovered)
{
    AnsiConsole.MarkupLine("[yellow]External tools could not be discovered; the built-in tools remain available.[/]");
    AnsiConsole.WriteLine(toolDiscoveryError ?? "Tool discovery failed.");
}
else if (externalToolCount > 0)
{
    AnsiConsole.MarkupLine($"[dim]Discovered {externalToolCount} external tool(s).[/]");
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
        AnsiConsole.MarkupLine("  [green]What is 12 multiplied by 3?[/] [dim]natural-language calculator request[/]");
        AnsiConsole.MarkupLine("  [green]Convert 150 cm to meters.[/] [dim]natural-language unit conversion[/]");
        AnsiConsole.MarkupLine("  [green]Multiply that by 5.[/] [dim]uses the latest numeric tool observation[/]");
        AnsiConsole.MarkupLine("  [green]:tool calculator {\"left\":12,\"operation\":\"multiply\",\"right\":3}[/] [dim]explicit diagnostic invocation[/]");
        AnsiConsole.MarkupLine("  [dim]Natural-language capability selection currently covers basic arithmetic and unit conversions.[/]");
        AnsiConsole.MarkupLine("  [dim]Tool results remain sourced observations; they are not added to session knowledge.[/]");
        AnsiConsole.MarkupLine("  [dim]External tools are discovered from ./tools or LATTICE_TOOLS_DIR and start only when invoked.[/]");
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
        AnsiConsole.MarkupLine("  [green]Fido likes the red ball.[/] [dim]descriptive relation[/]");
        AnsiConsole.MarkupLine("  [green]The red ball is shiny.[/] [dim]property assertion[/]");
        AnsiConsole.MarkupLine("  [green]Is the red ball red?[/]    [dim]property question[/]");
        AnsiConsole.MarkupLine("  [green]Does Fido like the red ball?[/] [dim]descriptive relation query[/]");
        AnsiConsole.MarkupLine("  [green]Does Fido like milk?[/]     [dim]relation question[/]");
        AnsiConsole.MarkupLine("  [green]Is Fido an animal?[/]       [dim]unary question[/]");
        AnsiConsole.MarkupLine("  [green]Is it a mammal?[/]          [dim]question with reference[/]");
        AnsiConsole.MarkupLine("[dim]Relation questions use the verb's base form; the parser maps it to third-person singular.[/]");
        AnsiConsole.MarkupLine("[dim]Relations remain exact lookups; universal rules infer unary predicates from one or more unary conditions.[/]");
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
        AnsiConsole.MarkupLine("[dim]LCE 0.8 is a small controlled-language subset, not full ACE or unrestricted English.[/]");
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
        AnsiConsole.MarkupLine("  [cyan]Language[/]             LCE 0.8 parser");
        AnsiConsole.MarkupLine("  [cyan]Semantics[/]            Terms, n-ary predicates, conjunction, existential and universal quantification");
        AnsiConsole.MarkupLine("  [cyan]Reasoning[/]            Conjunctive unary-rule chaining + existential lookup + direct relation lookup");
        AnsiConsole.MarkupLine($"  [cyan]Session knowledge[/]    {knowledgeBase.Facts.Count} facts, {knowledgeBase.Rules.Count} rules");
        AnsiConsole.MarkupLine($"  [cyan]Known individuals[/]    {conversationContext.KnownIndividualCount}");
        AnsiConsole.MarkupLine("  [cyan]Persistence[/]          [yellow]Current session only[/]");
        AnsiConsole.MarkupLine("  [cyan]Pack loading[/]         Version 1 JSON; imported claims retain pack provenance");
        AnsiConsole.MarkupLine("  [cyan]Provenance[/]            Structured sources; duplicate claims retain all sources");
        AnsiConsole.MarkupLine($"  [cyan]Knowledge packs[/]      {loadedPacks.Count} loaded");
        AnsiConsole.MarkupLine($"  [cyan]Tools[/]                {toolRegistry.Descriptors.Count} registered");
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
        var intent = intentResolution.Intent!;
        var result = await toolRegistry.TryInvokeAsync(intent.ToolId, intent.ArgumentsJson);

        if (!result.IsSuccess)
        {
            AnsiConsole.Markup("[bold red]Lattice[/] [dim]›[/] ");
            AnsiConsole.WriteLine(result.Error ?? "The capability could not complete the request.");
            AnsiConsole.WriteLine();
            continue;
        }

        conversationContext.Observe(result.Observation!);
        AnsiConsole.Markup("[bold cyan]Lattice[/] [dim]›[/] ");
        AnsiConsole.WriteLine(intent.FormatResponse(result.Observation!));

        var descriptor = toolRegistry.Descriptors.FirstOrDefault(
            item => string.Equals(item.Id, intent.ToolId, StringComparison.OrdinalIgnoreCase));
        AnsiConsole.MarkupLine(
            $"[dim]Used {descriptor?.Name ?? intent.ToolId}; result retained as a temporary observation.[/]");

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
