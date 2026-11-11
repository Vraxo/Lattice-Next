using Lattice.Core.Tools;
using Lattice.Translators.LmStudio;
using Spectre.Console;

namespace Lattice.Cli;

internal sealed class CliToolAccess
{
    private readonly IReadOnlyList<ITool> registeredTools;
    private readonly ToolPermission grantedPermissions;
    private readonly LmStudioConfigurationStore configurationStore;
    private readonly bool configurationLoaded;
    private LatticeConfiguration latticeConfiguration;
    private ToolRegistry toolRegistry;

    public CliToolAccess(
        IReadOnlyList<ITool> registeredTools,
        ToolPermission grantedPermissions,
        LmStudioConfigurationStore configurationStore,
        bool configurationLoaded,
        LatticeConfiguration latticeConfiguration)
    {
        this.registeredTools = registeredTools;
        this.grantedPermissions = grantedPermissions;
        this.configurationStore = configurationStore;
        this.configurationLoaded = configurationLoaded;
        this.latticeConfiguration = latticeConfiguration;
        rememberedToolPermissions = new Dictionary<string, ToolPermission>(
            latticeConfiguration.ToolPermissions,
            StringComparer.OrdinalIgnoreCase);
        sessionToolPermissions = new Dictionary<string, ToolPermission>(
            StringComparer.OrdinalIgnoreCase);
        toolRegistry = CreateToolRegistry();
    }

    private readonly Dictionary<string, ToolPermission> rememberedToolPermissions;
    private readonly Dictionary<string, ToolPermission> sessionToolPermissions;

    public Dictionary<string, ToolPermission> RememberedToolPermissions => rememberedToolPermissions;

    public Dictionary<string, ToolPermission> SessionToolPermissions => sessionToolPermissions;

    public ToolPermission GrantedPermissions => grantedPermissions;

    public ToolRegistry Registry => toolRegistry;

    public LatticeConfiguration Configuration => latticeConfiguration;

    public void UpdateConfiguration(LatticeConfiguration configuration)
    {
        latticeConfiguration = configuration;
    }

    public void RefreshRegistry()
    {
        toolRegistry = CreateToolRegistry();
    }

    private ToolRegistry CreateToolRegistry()
    {
        var combinedToolPermissions = new Dictionary<string, ToolPermission>(
            rememberedToolPermissions,
            StringComparer.OrdinalIgnoreCase);
    
        foreach (var pair in sessionToolPermissions)
        {
            combinedToolPermissions[pair.Key] = combinedToolPermissions.TryGetValue(
                pair.Key,
                out var remembered)
                ? remembered | pair.Value
                : pair.Value;
        }
    
        return new ToolRegistry(
            registeredTools,
            grantedPermissions,
            executionTimeout: TimeSpan.FromSeconds(15),
            toolPermissions: combinedToolPermissions);
    }
    
    public async ValueTask<ToolInvocationResult?> InvokeAsync(
        string requestedToolId,
        string argumentsJson)
    {
        var descriptor = toolRegistry.Descriptors.FirstOrDefault(
            item => string.Equals(item.Id, requestedToolId, StringComparison.OrdinalIgnoreCase));
    
        if (descriptor is not null)
        {
            var toolPermissions = rememberedToolPermissions.TryGetValue(
                descriptor.Id,
                out var remembered)
                ? remembered
                : ToolPermission.None;
            toolPermissions |= sessionToolPermissions.TryGetValue(
                descriptor.Id,
                out var session)
                ? session
                : ToolPermission.None;
    
            var missingPermissions = descriptor.RequiredPermissions &
                ~(grantedPermissions | toolPermissions);
    
            if (missingPermissions != ToolPermission.None)
            {
                var permissionNames = FormatPermissionNames(missingPermissions);
                var descriptions = new List<string>();
    
                if ((missingPermissions & ToolPermission.Read) != 0)
                {
                    descriptions.Add("read: access information outside this conversation");
                }
    
                if ((missingPermissions & ToolPermission.Write) != 0)
                {
                    descriptions.Add("write: make changes outside this conversation");
                }
    
                AnsiConsole.MarkupLine(
                    $"[yellow]{Markup.Escape(descriptor.Name)} requests {Markup.Escape(permissionNames)}.[/]");
                AnsiConsole.MarkupLine($"[dim]{Markup.Escape(string.Join("; ", descriptions))}.[/]");
                AnsiConsole.Markup(
                    "[yellow][[a]] Always allow / [[y]] This session / [[N]] Deny: [/]");
                var acceptance = Console.ReadLine()?.Trim();
    
                if (string.Equals(acceptance, "a", StringComparison.OrdinalIgnoreCase))
                {
                    var updatedPermissions = toolPermissions | missingPermissions;
                    var hadRememberedPermissions = rememberedToolPermissions.TryGetValue(
                        descriptor.Id,
                        out var previousRememberedPermissions);
                    rememberedToolPermissions[descriptor.Id] = updatedPermissions;
    
                    if (configurationLoaded)
                    {
                        var updatedConfiguration = latticeConfiguration with
                        {
                            ToolPermissions = new Dictionary<string, ToolPermission>(
                                rememberedToolPermissions,
                                StringComparer.OrdinalIgnoreCase)
                        };
    
                        try
                        {
                            await configurationStore.SaveAsync(updatedConfiguration);
                            latticeConfiguration = updatedConfiguration;
                            AnsiConsole.MarkupLine(
                                $"[dim]Remembered {Markup.Escape(permissionNames)} for {Markup.Escape(descriptor.Name)}.[/]");
                        }
                        catch (IOException)
                        {
                            RememberSessionPermission(descriptor.Id, missingPermissions);
                            RestoreRememberedPermissions(
                                descriptor.Id,
                                hadRememberedPermissions,
                                previousRememberedPermissions);
    
                            AnsiConsole.MarkupLine(
                                "[yellow]Could not save the approval; allowed for this session only.[/]");
                        }
                        catch (UnauthorizedAccessException)
                        {
                            RememberSessionPermission(descriptor.Id, missingPermissions);
                            RestoreRememberedPermissions(
                                descriptor.Id,
                                hadRememberedPermissions,
                                previousRememberedPermissions);
    
                            AnsiConsole.MarkupLine(
                                "[yellow]Could not save the approval; allowed for this session only.[/]");
                        }
                    }
                    else
                    {
                        RememberSessionPermission(descriptor.Id, missingPermissions);
                        RestoreRememberedPermissions(
                            descriptor.Id,
                            hadRememberedPermissions,
                            previousRememberedPermissions);
    
                        AnsiConsole.MarkupLine(
                            "[yellow]Configuration could not be loaded; allowed for this session only.[/]");
                    }
                }
                else if (string.Equals(acceptance, "y", StringComparison.OrdinalIgnoreCase))
                {
                    RememberSessionPermission(descriptor.Id, missingPermissions);
                    AnsiConsole.MarkupLine(
                        $"[dim]Allowed {Markup.Escape(permissionNames)} for {Markup.Escape(descriptor.Name)} until this session ends.[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[dim]Permission was not granted; the tool was not run.[/]");
                    return null;
                }
    
                toolRegistry = CreateToolRegistry();
            }
        }
    
        return await toolRegistry.TryInvokeAsync(requestedToolId, argumentsJson);
    }
    
    private void RestoreRememberedPermissions(
        string toolId,
        bool hadRememberedPermissions,
        ToolPermission previousPermissions)
    {
        if (hadRememberedPermissions)
        {
            rememberedToolPermissions[toolId] = previousPermissions;
        }
        else
        {
            rememberedToolPermissions.Remove(toolId);
        }
    }
    
    private void RememberSessionPermission(string toolId, ToolPermission permissions)
    {
        sessionToolPermissions[toolId] = sessionToolPermissions.TryGetValue(
            toolId,
            out var existing)
            ? existing | permissions
            : permissions;
    }
    
    public static string FormatPermissionNames(ToolPermission permissions)
    {
        var names = new List<string>(2);
    
        if ((permissions & ToolPermission.Read) != 0)
        {
            names.Add("read");
        }
    
        if ((permissions & ToolPermission.Write) != 0)
        {
            names.Add("write");
        }
    
        return string.Join(" and ", names);
    }
}
