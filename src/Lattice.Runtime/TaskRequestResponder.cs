using System.Collections.Immutable;
using Lattice.Client;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

namespace Lattice.Runtime;

internal static class TaskRequestResponder
{
    internal static bool TryRespond(
        TaskRequestFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(reasoner);

        return request.Action switch
        {
            TaskRequestAction.Write => TaskWritingResponder.TryWrite(request, model, reasoner, out message, out evidence),
            TaskRequestAction.Recommend => TaskRecommendationResponder.TryRecommend(request, model, reasoner, out message, out evidence),
            TaskRequestAction.Plan => request.OutputFormat == "learning-path"
                ? TaskLearningResponder.TryPlanLearningPath(request, model, reasoner, out message, out evidence)
                : TaskTravelResponder.TryPlanRoadTrip(request, model, reasoner, out message, out evidence),
            TaskRequestAction.Explain => TryExplain(request, model, reasoner, out message, out evidence),
            TaskRequestAction.Code => TaskProgramGenerationResponder.TryGenerateProgram(request, model, reasoner, out message, out evidence),
            TaskRequestAction.Brainstorm => TaskExplorationResponder.TryBrainstormUses(request, model, reasoner, out message, out evidence),
            TaskRequestAction.Explore => TaskExplorationResponder.TryExploreHobby(request, model, reasoner, out message, out evidence),
            _ => TaskRequestResponderHelpers.Fail(out message, out evidence)
        };
    }

    internal static bool TryExplain(
        TaskRequestFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        if (request.OutputFormat == "procedure")
        {
            return TaskOperationExplanationResponder.TryExplainOperation(request, model, reasoner, out message, out evidence);
        }

        if (request.OutputFormat == "conceptual-overview")
        {
            return TaskLearningResponder.TryExplainLearningMethod(request, model, reasoner, out message, out evidence);
        }

        return TaskRequestResponderHelpers.Fail(out message, out evidence);
    }
}
