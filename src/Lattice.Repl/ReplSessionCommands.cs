using Lattice.Core.Context;
using Lattice.Core.Intelligence;
using Lattice.Core.Reasoning;
using Lattice.Translators.LmStudio;

namespace Lattice.Repl;

internal sealed class ReplSessionCommands
{
    private readonly ReplPermissionCommands permissionCommands;
    private readonly ReplToolCommands toolCommands;
    private readonly ReplStatusCommand statusCommand;
    private readonly ReplToolIntentCommand toolIntentCommand;

    public ReplSessionCommands(
        ReplToolAccess toolAccess,
        ConversationContext conversationContext,
        KnowledgeBase knowledgeBase,
        List<KnowledgePack> loadedPacks,
        ToolIntentResolver toolIntentResolver,
        LmStudioConfigurationStore configurationStore,
        bool configurationLoaded,
        ReplTranslationCommands translationCommands)
    {
        permissionCommands = new ReplPermissionCommands(
            toolAccess,
            configurationStore,
            configurationLoaded,
            translationCommands);
        toolCommands = new ReplToolCommands(toolAccess, conversationContext, translationCommands);
        statusCommand = new ReplStatusCommand(
            knowledgeBase,
            conversationContext,
            loadedPacks,
            toolAccess,
            translationCommands);
        toolIntentCommand = new ReplToolIntentCommand(
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
