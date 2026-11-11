using Lattice.Core.Intelligence;
using Lattice.Core.Semantics;
using Lattice.Translators.LmStudio;
using Spectre.Console;

namespace Lattice.Repl;

internal sealed class ReplTranslationCommands
{
    private readonly LmStudioConfigurationStore configurationStore;
    private readonly LmStudioRuntime lmStudioRuntime;
    private readonly Func<LmStudioConfiguration, SemanticTranslationService?> createTranslationService;
    private readonly Action<string> reportStatus;
    private readonly ReplUtteranceProcessor utteranceProcessor;
    private LatticeConfiguration configuration;
    private SemanticTranslationService? translationService;

    public ReplTranslationCommands(
        LatticeConfiguration configuration,
        SemanticTranslationService? translationService,
        LmStudioConfigurationStore configurationStore,
        LmStudioRuntime lmStudioRuntime,
        Func<LmStudioConfiguration, SemanticTranslationService?> createTranslationService,
        Action<string> reportStatus,
        ReplUtteranceProcessor utteranceProcessor)
    {
        this.configuration = configuration;
        this.translationService = translationService;
        this.configurationStore = configurationStore;
        this.lmStudioRuntime = lmStudioRuntime;
        this.createTranslationService = createTranslationService;
        this.reportStatus = reportStatus;
        this.utteranceProcessor = utteranceProcessor;
    }

    public LatticeConfiguration Configuration => configuration;

    public SemanticTranslationService? TranslationService => translationService;

    public void UpdateConfiguration(LatticeConfiguration updatedConfiguration)
    {
        configuration = updatedConfiguration;
    }

    public async ValueTask<bool> TryHandleAsync(string input)
    {
        if (string.Equals(input, ":translator", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine("[bold]Local semantics translator[/]");
            AnsiConsole.MarkupLine($"  [cyan]Configuration[/]  {configurationStore.Path}");
            AnsiConsole.MarkupLine($"  [cyan]API[/]            {configuration.LmStudio.ApiUrl}");
            AnsiConsole.MarkupLine($"  [cyan]LM Studio CLI[/]  {configuration.LmStudio.LmsExecutable}");

            if (string.IsNullOrWhiteSpace(configuration.LmStudio.ModelKey))
            {
                AnsiConsole.MarkupLine("  [yellow]No model selected[/]");
                AnsiConsole.MarkupLine("  Run :translator setup to discover and select an installed LLM.");
            }
            else
            {
                AnsiConsole.MarkupLine($"  [cyan]Model key[/]      {configuration.LmStudio.ModelKey}");
                AnsiConsole.MarkupLine($"  [cyan]Model ID[/]       {configuration.LmStudio.ModelIdentifier ?? "(assigned when loaded)"}");
                AnsiConsole.MarkupLine("  [dim]The server and model are checked and started automatically when you translate.[/]");
            }

            AnsiConsole.WriteLine();
            return true;
        }

        if (input.Equals(":translator setup", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var models = await lmStudioRuntime.ListInstalledModelsAsync(
                    reportStatus);

                if (models.IsEmpty)
                {
                    AnsiConsole.MarkupLine("[yellow]No installed language models were found in LM Studio.[/]");
                    AnsiConsole.MarkupLine("[dim]Download or import a GGUF model in LM Studio, then run :translator setup again.[/]");
                    AnsiConsole.WriteLine();
                    return true;
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
                    return true;
                }

                var selectedModel = models[selectedIndex - 1];
                var selectedConfiguration = configuration.LmStudio with
                {
                    ModelKey = selectedModel.Key,
                    ModelIdentifier = selectedModel.LoadedInstanceIds.IsDefaultOrEmpty
                        ? null
                        : selectedModel.LoadedInstanceIds[0]
                };
                var updatedConfiguration = configuration with
                {
                    LmStudio = selectedConfiguration
                };

                await configurationStore.SaveAsync(updatedConfiguration);
                configuration = updatedConfiguration;

                translationService = createTranslationService(selectedConfiguration);

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
            return true;
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
                return true;
            }

            if (translationService is null)
            {
                AnsiConsole.MarkupLine("[bold yellow]Lattice[/] [dim]›[/] Local translation is not configured. Run :translator setup first.");
                AnsiConsole.WriteLine();
                return true;
            }

            var translation = await translationService.TranslateAsync(sourceText);

            if (translation.Status == SemanticTranslationStatus.ReadyForReview)
            {
                var candidate = translation.Candidates[0];
                AnsiConsole.MarkupLine("[bold cyan]Lattice[/] [dim]› Proposed meaning (not yet accepted):[/]");
                AnsiConsole.WriteLine(SemanticFormatter.Format(candidate));
                AnsiConsole.Markup("[yellow]Accept and process this interpretation? [[y/N]] [/]");
                var acceptance = Console.ReadLine();

                if (string.Equals(acceptance?.Trim(), "y", StringComparison.OrdinalIgnoreCase))
                {
                    await utteranceProcessor.ProcessAsync(candidate);
                }
                else
                {
                    AnsiConsole.MarkupLine("[dim]Candidate discarded; session knowledge was not changed.[/]");
                }

                AnsiConsole.WriteLine();
                return true;
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
                    await utteranceProcessor.ProcessAsync(translation.Candidates[selectedIndex - 1]);
                }
                else
                {
                    AnsiConsole.MarkupLine("[dim]No candidate accepted; session knowledge was not changed.[/]");
                }

                AnsiConsole.WriteLine();
                return true;
            }

            AnsiConsole.Markup("[bold yellow]Lattice[/] [dim]›[/] ");
            AnsiConsole.WriteLine(translation.Message ?? translation.Status.ToString());
            AnsiConsole.WriteLine();
            return true;
        }

    return false;
}
}
