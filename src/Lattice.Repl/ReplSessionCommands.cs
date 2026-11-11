using Lattice.Client;
using Lattice.Core.Context;
using Lattice.Core.Intelligence;
using Lattice.Translators.LmStudio;

namespace Lattice.Repl;

internal sealed class ReplSessionCommands
{
    private readonly ReplPermissionCommands permissionCommands;
    private readonly ReplToolCommands toolCommands;
    private readonly ReplStatusCommand statusCommand;
    private readonly ReplToolIntentCommand toolIntentCommand;

    public ReplSessionCommands(
        ILatticeSession session,
        ConversationContext conversationContext,
        ReplToolAccess toolAccess,
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
            session,
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
