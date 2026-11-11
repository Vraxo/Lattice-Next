using Lattice.Core.Context;
using Lattice.Core.Intelligence;
using Lattice.Core.Reasoning;
using Lattice.Translators.LmStudio;

namespace Lattice.Cli;

internal sealed class CliSessionCommands
{
    private readonly CliPermissionCommands permissionCommands;
    private readonly CliToolCommands toolCommands;
    private readonly CliStatusCommand statusCommand;
    private readonly CliToolIntentCommand toolIntentCommand;

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
        permissionCommands = new CliPermissionCommands(
            toolAccess,
            configurationStore,
            configurationLoaded,
            translationCommands);
        toolCommands = new CliToolCommands(toolAccess, conversationContext, translationCommands);
        statusCommand = new CliStatusCommand(
            knowledgeBase,
            conversationContext,
            loadedPacks,
            toolAccess,
            translationCommands);
        toolIntentCommand = new CliToolIntentCommand(
            toolAccess,
            conversationContext,
            toolIntentResolver,
            translationCommands);
    }

    public async ValueTask<bool> TryHandleAsync(string input)
    {
        if (await permissionCommands.TryHandleAsync(input))
        {
            return true;
        }

        if (await toolCommands.TryHandleAsync(input))
        {
            return true;
        }

        if (statusCommand.TryHandle(input))
        {
            return true;
        }

        return await toolIntentCommand.TryHandleAsync(input);
    }
}
