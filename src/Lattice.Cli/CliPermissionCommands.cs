using Lattice.Core.Tools;
using Lattice.Translators.LmStudio;
using Spectre.Console;

namespace Lattice.Cli;

internal sealed class CliPermissionCommands(
    CliToolAccess toolAccess,
    LmStudioConfigurationStore configurationStore,
    bool configurationLoaded,
    CliTranslationCommands translationCommands)
{
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

        return false;
    }
}
