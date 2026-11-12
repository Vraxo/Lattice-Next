using Lattice.Client;
using Lattice.Core.Context;
using Lattice.Core.Intelligence;
using Lattice.Translators.LmStudio;

namespace Lattice.Repl;

internal sealed class ReplSessionCommands
{
    private readonly ReplPermissionCommands permissionCommands;
    private readonly ReplToolCommands toolCommands;
    private readonly ReplCandidateEvidenceCommands candidateEvidenceCommands;
    private readonly ReplStatusCommand statusCommand;
    private readonly ReplObjectiveExplanationCommand objectiveExplanationCommand;
    private readonly ReplToolIntentCommand toolIntentCommand;

    public ReplSessionCommands(
        ReplToolAccess toolAccess,
        ConversationContext conversationContext,
        ILatticeSession session,
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
        candidateEvidenceCommands = new ReplCandidateEvidenceCommands(session, conversationContext);
        statusCommand = new ReplStatusCommand(
            session,
            toolAccess,
            translationCommands);
        objectiveExplanationCommand = new ReplObjectiveExplanationCommand(session);
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

        if (await candidateEvidenceCommands.TryHandleAsync(input))
        {
            return true;
        }

        if (await statusCommand.TryHandleAsync(input))
        {
            return true;
        }

        if (await objectiveExplanationCommand.TryHandleAsync(input))
        {
            return true;
        }

        return await toolIntentCommand.TryHandleAsync(input);
    }
}
