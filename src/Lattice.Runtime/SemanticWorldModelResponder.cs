using System.Collections.Immutable;
using System.Globalization;
using Lattice.Client;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

namespace Lattice.Runtime;

internal static class SemanticWorldModelResponder
{

    internal static bool TryRespond(ChargedParticleAccelerationFormula request, SemanticWorldModel model, SemanticWorldModelReasoner reasoner, out string message, out ImmutableArray<LatticeEvidence> evidence) =>
        ChargedParticleAccelerationResponder.TryRespond(request, model, reasoner, out message, out evidence);

    internal static bool TryRespond(SemanticRelationQueryFormula request, SemanticWorldModel model, SemanticWorldModelReasoner reasoner, out string message, out ImmutableArray<LatticeEvidence> evidence) =>
        SemanticRelationResponder.TryRespond(request, model, reasoner, out message, out evidence);

    internal static bool TryRespond(SemanticRelationSourceQueryFormula request, SemanticWorldModel model, SemanticWorldModelReasoner reasoner, out string message, out ImmutableArray<LatticeEvidence> evidence) =>
        SemanticRelationSourceResponder.TryRespond(request, model, reasoner, out message, out evidence);

    internal static bool TryDefinition(string concept, SemanticWorldModel model, SemanticWorldModelReasoner reasoner, out string message, out ImmutableArray<LatticeEvidence> evidence) =>
        SemanticDefinitionResponder.TryDefinition(concept, model, reasoner, out message, out evidence);

    internal static bool TryClassify(MultiItemClassificationFormula request, SemanticWorldModel model, SemanticWorldModelReasoner reasoner, out string message, out ImmutableArray<LatticeEvidence> evidence) =>
        SemanticClassificationResponder.TryClassify(request, model, reasoner, out message, out evidence);

    internal static bool TryChoice(ChoiceQuestionFormula question, SemanticWorldModel model, SemanticWorldModelReasoner reasoner, out string message, out ImmutableArray<LatticeEvidence> evidence) =>
        SemanticClassificationResponder.TryChoice(question, model, reasoner, out message, out evidence);

    internal static bool TryAskMembership(string individual, string type, SemanticWorldModel model, SemanticWorldModelReasoner reasoner, out ImmutableArray<LatticeEvidence> evidence) =>
        SemanticClassificationResponder.TryAskMembership(individual, type, model, reasoner, out evidence);

    internal static bool TryRespond(
        SemanticWorldModelQuestionFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(reasoner);

        return request.Kind switch
        {
            SemanticWorldModelQuestionKind.Definition =>
                SemanticDefinitionResponder.TryDefinition(request.Concept ?? request.Subject, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.WhyClassification =>
                SemanticConceptExplanationResponder.TryWhy(request.Subject, request.Concept ?? string.Empty, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.CapabilityExplanation =>
                SemanticExplanationResponder.TryExplainCapability(request.Subject, request.Concept ?? string.Empty, request.OtherSubject, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.Ability =>
                SemanticExplanationResponder.TryExplainAbility(request.Subject, request.Concept ?? string.Empty, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.Mechanism =>
                SemanticConceptExplanationResponder.TryExplainMechanism(request.Subject, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.Causes =>
                SemanticExplanationResponder.TryExplainCauses(request.Subject, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.Compare =>
                SemanticComparisonResponder.TryCompare(request.Subject, request.OtherSubject ?? string.Empty, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.SamplingParameters =>
                SemanticApplicationResponder.TryExplainSamplingParameters(request.Subject, request.OtherSubject ?? string.Empty, request.TargetType ?? string.Empty, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.WhyUse =>
                SemanticApplicationResponder.TryWhyUse(request.Subject, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.Applications =>
                SemanticApplicationResponder.TryExplainApplications(request.Subject, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.RequiredKnowledge =>
                SemanticConceptExplanationResponder.TryExplainRequiredKnowledge(request.Subject, request.Concept ?? string.Empty, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.ContributionQuery =>
                SemanticDefinitionResponder.TryContributionQuery(request, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.WhyHarmful =>
                SemanticExplanationResponder.TryExplainHarmfulness(request.Subject, request.Concept ?? string.Empty, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.BeginnerGuidance =>
                SemanticGuidanceResponder.TryBeginnerGuidance(request.Subject, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.CreativeIdeas =>
                SemanticGuidanceResponder.TryCreativeIdeas(request.Subject, request.OtherSubject, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.ListExamples =>
                SemanticDataQueryResponder.TryListExamples(request.Subject, request.Limit, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.TypeInstanceListQuery =>
                SemanticDataQueryResponder.TryListTypeInstances(request.Subject, request.TargetType ?? string.Empty, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.MaximumDataValue =>
                SemanticDataQueryResponder.TryMaximumDataValue(request, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.HighestDataValue =>
                SemanticDataQueryResponder.TryHighestDataValue(request, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.NotableExamples =>
                SemanticDataQueryResponder.TryNotableExamples(request, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.RegionalName =>
                SemanticContextResponder.TryRegionalName(request, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.Ingredients =>
                SemanticContextResponder.TryIngredients(request, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.EventOutcomes =>
                SemanticDefinitionResponder.TryExplainEventOutcomes(request.Subject, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.ContextualRelationRoleQuery =>
                SemanticContextResponder.TryContextualRelationRoleQuery(request.Subject, request.Concept ?? string.Empty, request.OtherSubject ?? string.Empty, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.ProductionLocationReasons =>
                SemanticContextResponder.TryExplainProductionLocationReasons(request.Subject, request.OtherSubject ?? string.Empty, model, reasoner, out message, out evidence),
            _ => SemanticResponseFormatting.Fail(out message, out evidence)
        };
    }
}
