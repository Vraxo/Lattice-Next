using System.Collections.Immutable;
using System.Globalization;
using Lattice.Client;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

using static Lattice.Runtime.SemanticResponseFormatting;
using static Lattice.Runtime.SemanticWorldModelResponseReasoningHelpers;
namespace Lattice.Runtime;

internal static class SemanticApplicationResponder
{

    internal static bool TryExplainApplications(
        string requestedMethod,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        var method = model.FindConcept(requestedMethod, SemanticConceptKind.Individual);
        if (method is null || !reasoner.IsInstanceOf(method.Id, "limit-evaluation-method"))
        {
            return false;
        }

        var purposeAssertions = reasoner.GetRelations(method.Id, "has-purpose");
        var formAssertions = reasoner.GetRelations(method.Id, "applies-to-indeterminate-form");
        var conditionAssertions = reasoner.GetRelations(method.Id, "requires-condition");
        var applicationAssertions = reasoner.GetRelations(method.Id, "broader-calculus-context");
        if (purposeAssertions.Length != 1 ||
            formAssertions.Length != 2 ||
            conditionAssertions.Length != 3 ||
            applicationAssertions.Length != 4)
        {
            return false;
        }

        var purpose = model.FindConcept(purposeAssertions[0].Object, SemanticConceptKind.Individual);
        var forms = formAssertions
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .ToArray();
        var conditions = conditionAssertions
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .ToArray();
        var applications = applicationAssertions
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .ToArray();
        if (purpose is null ||
            forms.Any(concept => concept is null) ||
            conditions.Any(concept => concept is null) ||
            applications.Any(concept => concept is null))
        {
            return false;
        }

        var formNames = forms.Select(concept => concept!.Label).ToArray();
        var conditionNames = conditions.Select(concept => concept!.Label).ToArray();
        var applicationNames = applications.Select(concept => concept!.Label).ToArray();
        var sources = purposeAssertions
            .Concat(formAssertions)
            .Concat(conditionAssertions)
            .Concat(applicationAssertions)
            .Select(assertion => assertion.Source)
            .Concat(reasoner.GetMembershipEvidence(method.Id, "limit-evaluation-method"))
            .Distinct()
            .ToImmutableArray();

        message = $"{method.Label} is a calculus method for {purpose.Label}. It applies when direct substitution gives {JoinNames(formNames)}. The rule requires that {JoinNames(conditionNames)}; when those conditions hold, the limit can be evaluated using the quotient of the derivatives of the numerator and denominator. In day-to-day life, you are unlikely to apply the rule directly. The broader calculus of limits and derivatives supports {JoinNames(applicationNames)}. These calculations are usually handled in mathematical models or by software; ordinary tasks such as working out a bill or an average travel time do not require L'Hôpital's Rule.";
        evidence = ImmutableArray.Create(ToEvidence(sources, LatticeEvidenceKind.Derived));
        return true;
    }

    internal static bool TryWhyUse(
        string requestedAssistant,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        var assistant = model.FindConcept(requestedAssistant, SemanticConceptKind.Individual);
        if (assistant is null ||
            !reasoner.IsInstanceOf(assistant.Id, "conversational-ai-assistant"))
        {
            return false;
        }

        var benefitAssertions = reasoner.GetRelations(assistant.Id, "offers-benefit");
        if (benefitAssertions.IsDefaultOrEmpty ||
            !TryGetSingleTarget("has-project-status", out var projectStatus, out var statusSource) ||
            !TryGetSingleTarget("has-access-model", out var accessModel, out var accessSource) ||
            !TryGetSingleTarget("has-licensing-model", out var licensingModel, out var licensingSource) ||
            !TryGetSingleTarget("has-demo-status", out var demoStatus, out var demoSource) ||
            !TryGetSingleTarget("has-model-quality", out var modelQuality, out var qualitySource) ||
            !TryGetSingleTarget("has-hardware-requirement", out var hardwareRequirement, out var hardwareSource))
        {
            return false;
        }

        var benefits = benefitAssertions
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .Where(concept => concept is not null)
            .Select(concept => concept!.Label)
            .ToArray();
        if (benefits.Length == 0)
        {
            return false;
        }

        var sources = benefitAssertions
            .Select(assertion => assertion.Source)
            .Append(statusSource)
            .Append(accessSource)
            .Append(licensingSource)
            .Append(demoSource)
            .Append(qualitySource)
            .Append(hardwareSource)
            .Distinct()
            .ToImmutableArray();

        message = $"{assistant.Label} is worth exploring for its {JoinNames(benefits)}. It is {IndefinitePhrase(projectStatus)}, and its {demoStatus}. You can use its {accessModel}. Its licensing terms: {licensingModel}. {Capitalize(modelQuality)}; {hardwareRequirement}. It is best suited to research and self-hosted experimentation, not as a maintained hosted chatbot.";
        evidence = ImmutableArray.Create(ToEvidence(sources, LatticeEvidenceKind.Derived));
        return true;

        bool TryGetSingleTarget(
            string relation,
            out string label,
            out SemanticWorldModelSource source)
        {
            label = string.Empty;
            source = new SemanticWorldModelSource(string.Empty);
            var assertions = reasoner.GetRelations(assistant.Id, relation);
            if (assertions.Length != 1)
            {
                return false;
            }

            var target = model.FindConcept(assertions[0].Object, SemanticConceptKind.Individual);
            if (target is null)
            {
                return false;
            }

            label = target.Label;
            source = assertions[0].Source;
            return true;
        }
    }

    internal static bool TryExplainSamplingParameters(
        string firstParameterId,
        string secondParameterId,
        string domainId,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        var first = model.FindConcept(firstParameterId, SemanticConceptKind.Individual);
        var second = model.FindConcept(secondParameterId, SemanticConceptKind.Individual);
        var domain = model.FindConcept(domainId, SemanticConceptKind.Type);
        if (first is null ||
            second is null ||
            domain is null ||
            !reasoner.IsInstanceOf(first.Id, "llm-sampling-parameter") ||
            !reasoner.IsInstanceOf(second.Id, "llm-sampling-parameter"))
        {
            return false;
        }

        SemanticRelationAssertion? GetFirst(string subject, string relation) =>
            reasoner.GetRelations(subject, relation).FirstOrDefault();

        var firstModel = GetFirst(first.Id, "llm-parameter-of");
        var secondModel = GetFirst(second.Id, "llm-parameter-of");
        if (firstModel is null ||
            secondModel is null ||
            firstModel.Object != secondModel.Object ||
            !reasoner.IsInstanceOf(firstModel.Object, domain.Id))
        {
            return false;
        }

        var firstLowerEffect = GetFirst(first.Id, "llm-lower-setting-effect");
        var secondLowerEffect = GetFirst(second.Id, "llm-lower-setting-effect");
        var temperature = firstLowerEffect is not null ? first : second;
        var topP = firstLowerEffect is not null ? second : first;
        var lowerEffectAssertion = firstLowerEffect ?? secondLowerEffect;
        var higherEffectAssertion = GetFirst(temperature.Id, "llm-higher-setting-effect");
        var temperatureMethodAssertion = GetFirst(temperature.Id, "llm-uses-method");
        var topPMethodAssertion = GetFirst(topP.Id, "llm-uses-method");
        if (lowerEffectAssertion is null ||
            higherEffectAssertion is null ||
            temperatureMethodAssertion is null ||
            topPMethodAssertion is null)
        {
            return false;
        }

        var temperatureMethod = model.FindConcept(temperatureMethodAssertion.Object, SemanticConceptKind.Individual);
        var topPMethod = model.FindConcept(topPMethodAssertion.Object, SemanticConceptKind.Individual);
        if (temperatureMethod is null || topPMethod is null)
        {
            return false;
        }

        var temperatureInputAssertion = GetFirst(temperatureMethod.Id, "llm-method-rescales");
        var temperatureStageAssertion = GetFirst(temperatureMethod.Id, "llm-method-before");
        var topPInputAssertion = GetFirst(topPMethod.Id, "llm-method-acts-on");
        var topPStageAssertion = GetFirst(topPMethod.Id, "llm-method-after");
        var topPOrderAssertion = GetFirst(topPMethod.Id, "llm-method-orders-by");
        var topPNucleusAssertion = GetFirst(topPMethod.Id, "llm-method-retains");
        var topPRenormalizationAssertion = GetFirst(topPMethod.Id, "llm-method-renormalizes");
        if (temperatureInputAssertion is null ||
            temperatureStageAssertion is null ||
            topPInputAssertion is null ||
            topPStageAssertion is null ||
            topPOrderAssertion is null ||
            topPNucleusAssertion is null ||
            topPRenormalizationAssertion is null)
        {
            return false;
        }

        var temperatureInput = model.FindConcept(temperatureInputAssertion.Object, SemanticConceptKind.Individual);
        var temperatureStage = model.FindConcept(temperatureStageAssertion.Object, SemanticConceptKind.Individual);
        var topPInput = model.FindConcept(topPInputAssertion.Object, SemanticConceptKind.Individual);
        var topPStage = model.FindConcept(topPStageAssertion.Object, SemanticConceptKind.Individual);
        var orderedBy = model.FindConcept(topPOrderAssertion.Object, SemanticConceptKind.Individual);
        var nucleus = model.FindConcept(topPNucleusAssertion.Object, SemanticConceptKind.Individual);
        var renormalized = model.FindConcept(topPRenormalizationAssertion.Object, SemanticConceptKind.Individual);
        var lowerEffect = model.FindConcept(lowerEffectAssertion.Object, SemanticConceptKind.Individual);
        var higherEffect = model.FindConcept(higherEffectAssertion.Object, SemanticConceptKind.Individual);
        if (temperatureInput is null ||
            temperatureStage is null ||
            topPInput is null ||
            topPStage is null ||
            orderedBy is null ||
            nucleus is null ||
            renormalized is null ||
            lowerEffect is null ||
            higherEffect is null)
        {
            return false;
        }

        if (!SamplingParameterCalculator.TryCalculateTemperatureDistribution(
                [2d, 1d, 0d],
                0.5d,
                out var lowTemperature,
                out _) ||
            !SamplingParameterCalculator.TryCalculateTemperatureDistribution(
                [2d, 1d, 0d],
                2d,
                out var highTemperature,
                out _) ||
            !SamplingParameterCalculator.TrySelectNucleus(
                [0.6d, 0.3d, 0.1d],
                0.8d,
                out var retainedIndices,
                out var retainedProbabilities,
                out _))
        {
            return false;
        }

        var retainedTokens = JoinNames(retainedIndices.Select(index => $"token {index + 1}").ToArray());
        var lowTemperatureExample = string.Join(", ", lowTemperature.Select(FormatPercentage));
        var highTemperatureExample = string.Join(", ", highTemperature.Select(FormatPercentage));
        var retainedExample = string.Join(" and ", retainedProbabilities.Select(FormatPercentage));
        var temperatureSentence =
            $"{Capitalize(temperature.Label)} uses {temperatureMethod.Label}. " +
            $"{Capitalize(temperatureMethod.Label)} {model.FindRelation(temperatureInputAssertion.Relation)!.Verb} {temperatureInput.Label} by dividing each logit by T " +
            $"and {model.FindRelation(temperatureStageAssertion.Relation)!.Verb} {temperatureStage.Label}. " +
            $"Lower temperature {model.FindRelation(lowerEffectAssertion.Relation)!.Verb}; " +
            $"higher temperature {model.FindRelation(higherEffectAssertion.Relation)!.Verb}.";
        var topPSentence =
            $"{Capitalize(topP.Label)} uses {topPMethod.Label}. " +
            $"{Capitalize(topPMethod.Label)} {model.FindRelation(topPInputAssertion.Relation)!.Verb} {topPInput.Label} " +
            $"and {model.FindRelation(topPStageAssertion.Relation)!.Verb} {topPStage.Label}. " +
            $"It {model.FindRelation(topPOrderAssertion.Relation)!.Verb} {orderedBy.Label}, " +
            $"{model.FindRelation(topPNucleusAssertion.Relation)!.Verb} {nucleus.Label}, " +
            $"then {model.FindRelation(topPRenormalizationAssertion.Relation)!.Verb} {renormalized.Label}.";

        message =
            $"{Capitalize(temperature.Label)} and {topP.Label} are both sampling parameters of a language model, but they affect token selection differently. " +
            $"{temperatureSentence} {topPSentence} " +
            $"For example, with next-token logits [2, 1, 0], temperature 0.5 gives probabilities {lowTemperatureExample}, " +
            $"while temperature 2 gives {highTemperatureExample}. " +
            $"With token probabilities 0.6, 0.3, and 0.1 and top-p = 0.8, nucleus sampling keeps {retainedTokens} " +
            $"because their cumulative probability reaches 0.9, then renormalizes them to {retainedExample}. " +
            "In short, temperature changes how concentrated the probability distribution is; top-p changes which tokens remain eligible.";

        var sources = new[]
        {
            firstModel.Source,
            secondModel.Source,
            temperatureMethodAssertion.Source,
            topPMethodAssertion.Source,
            temperatureInputAssertion.Source,
            temperatureStageAssertion.Source,
            topPInputAssertion.Source,
            topPStageAssertion.Source,
            topPOrderAssertion.Source,
            topPNucleusAssertion.Source,
            topPRenormalizationAssertion.Source,
            lowerEffectAssertion.Source,
            higherEffectAssertion.Source
        }.Distinct().ToImmutableArray();
        evidence = ImmutableArray.Create(ToEvidence(sources, LatticeEvidenceKind.Derived));
        return true;
    }
}
