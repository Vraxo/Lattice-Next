using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class SemanticWorldModelQuestionParser
{
    internal static bool TryParse(string source, out SemanticUtterance? utterance)
    {
        utterance = null;
        var text = source.Trim();

        if (SemanticWorldModelRelationQuestionParser.TryParsePair(source, text, out utterance))
        {
            return true;
        }

        var chargedParticleAcceleration = SemanticWorldModelQuestionPatterns.ChargedParticleAccelerationPattern.Match(text);
        if (chargedParticleAcceleration.Success &&
            TryParseScientificNumber(chargedParticleAcceleration.Groups["speed"].Value, out var initialSpeed) &&
            decimal.TryParse(
                chargedParticleAcceleration.Groups["potential"].Value.Replace('−', '-').Replace('‐', '-').Replace('‑', '-').Replace('–', '-').Replace('—', '-'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var potentialDifference))
        {
            utterance = new SemanticUtterance(
                UtteranceKind.Question,
                new ChargedParticleAccelerationFormula(
                    SemanticWorldModelQuestionNormalization.Normalize(chargedParticleAcceleration.Groups["particle"].Value),
                    initialSpeed,
                    potentialDifference),
                source);
            return true;
        }

        var samplingParameters = SemanticWorldModelQuestionPatterns.SamplingParametersPattern.Match(text);
        if (samplingParameters.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.SamplingParameters,
                subject: SemanticWorldModelQuestionNormalization.Normalize(samplingParameters.Groups["first"].Value),
                otherSubject: SemanticWorldModelQuestionNormalization.Normalize(samplingParameters.Groups["second"].Value),
                targetType: SemanticWorldModelQuestionNormalization.Normalize(samplingParameters.Groups["domain"].Value),
                sourceText: source);
            return true;
        }

        var productionLocationReasons = SemanticWorldModelQuestionPatterns.ProductionLocationReasonsPattern.Match(text);
        if (productionLocationReasons.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.ProductionLocationReasons,
                subject: SemanticWorldModelQuestionNormalization.Normalize(productionLocationReasons.Groups["activity"].Value),
                otherSubject: SemanticWorldModelQuestionNormalization.Normalize(productionLocationReasons.Groups["location"].Value),
                sourceText: source);
            return true;
        }

        var contextualRole = SemanticWorldModelQuestionPatterns.ContextualRelationRolePattern.Match(text);
        if (contextualRole.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.ContextualRelationRoleQuery,
                subject: SemanticWorldModelQuestionNormalization.Normalize(contextualRole.Groups["possessor"].Value),
                concept: SemanticWorldModelQuestionNormalization.Normalize(contextualRole.Groups["role"].Value),
                otherSubject: SemanticWorldModelQuestionNormalization.Normalize(contextualRole.Groups["context"].Value),
                sourceText: source);
            return true;
        }

        var commonIngredients = SemanticWorldModelQuestionPatterns.CommonIngredientsPattern.Match(text);
        if (commonIngredients.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.Ingredients,
                subject: SemanticWorldModelQuestionNormalization.Normalize(commonIngredients.Groups["dish"].Value),
                sourceText: source);
            return true;
        }

        var regionalName = SemanticWorldModelQuestionPatterns.RegionalNamePattern.Match(text);
        if (regionalName.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.RegionalName,
                subject: SemanticWorldModelQuestionNormalization.Normalize(regionalName.Groups["subject"].Value),
                targetType: SemanticWorldModelQuestionNormalization.NormalizeRegionalVariant(regionalName.Groups["region"].Value),
                sourceText: source);
            return true;
        }

        var missingListItems = SemanticWorldModelQuestionPatterns.MissingListItemsPattern.Match(text);
        if (missingListItems.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.Clarification,
                subject: SemanticWorldModelQuestionNormalization.Normalize(missingListItems.Groups["items"].Value),
                otherSubject: $"{missingListItems.Groups["list"].Value.Trim()} list",
                sourceText: source);
            return true;
        }

        var companyPurpose = SemanticWorldModelQuestionPatterns.CompanyPurposeQuestionPattern.Match(text);
        if (companyPurpose.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.CreativeIdeas,
                subject: "business-ideas",
                otherSubject: companyPurpose.Groups["company"].Value.Trim(),
                sourceText: source);
            return true;
        }

        var requiredKnowledge = SemanticWorldModelQuestionPatterns.RequiredKnowledgePattern.Match(text);
        if (requiredKnowledge.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.RequiredKnowledge,
                subject: SemanticWorldModelQuestionNormalization.Normalize(requiredKnowledge.Groups["activity"].Value),
                concept: SemanticWorldModelQuestionNormalization.Normalize(requiredKnowledge.Groups["knowledge"].Value),
                sourceText: source);
            return true;
        }

        var everydayApplications = SemanticWorldModelQuestionPatterns.EverydayApplicationsPattern.Match(text);
        if (everydayApplications.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.Applications,
                subject: SemanticWorldModelQuestionNormalization.Normalize(everydayApplications.Groups["subject"].Value),
                sourceText: source);
            return true;
        }

        var difference = SemanticWorldModelQuestionPatterns.DifferenceBetweenPattern.Match(text);
        if (difference.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.Compare,
                subject: SemanticWorldModelQuestionNormalization.Normalize(difference.Groups["subject"].Value),
                otherSubject: SemanticWorldModelQuestionNormalization.Normalize(difference.Groups["other"].Value),
                sourceText: source);
            return true;
        }

        var whyShouldUse = SemanticWorldModelQuestionPatterns.WhyShouldUsePattern.Match(text);
        if (whyShouldUse.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.WhyUse,
                subject: SemanticWorldModelQuestionNormalization.Normalize(whyShouldUse.Groups["subject"].Value),
                sourceText: source);
            return true;
        }

        var similarChatbots = SemanticWorldModelQuestionPatterns.SimilarChatbotsPattern.Match(text);
        if (similarChatbots.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.ListExamples,
                subject: "advanced-chatbot",
                limit: 5,
                sourceText: source);
            return true;
        }

        var enumerationRemainder = SemanticWorldModelQuestionPatterns.EnumerationRemainderPattern.Match(text);
        if (enumerationRemainder.Success &&
            SemanticWorldModelQuestionNormalization.TryParseCardinal(enumerationRemainder.Groups["count"].Value, out var expectedCount) &&
            SemanticWorldModelQuestionNormalization.TryParseOrdinal(enumerationRemainder.Groups["ordinal"].Value, out var missingPosition))
        {
            var namedMembers = enumerationRemainder.Groups["known"].Value
                .Split(", ", StringSplitOptions.RemoveEmptyEntries)
                .Select(SemanticWorldModelQuestionNormalization.Normalize)
                .ToImmutableArray();
            var member = SemanticWorldModelQuestionNormalization.Normalize(enumerationRemainder.Groups["member"].Value);
            var group = SemanticWorldModelQuestionNormalization.Normalize(enumerationRemainder.Groups["group"].Value);
            var questionGroup = SemanticWorldModelQuestionNormalization.Normalize(enumerationRemainder.Groups["questionGroup"].Value);

            if (expectedCount > 0 &&
                missingPosition == expectedCount &&
                SemanticWorldModelQuestionNormalization.SingularizeEnumerationGroup(group) == SemanticWorldModelQuestionNormalization.SingularizeEnumerationGroup(questionGroup) &&
                namedMembers.Length == expectedCount - 1 &&
                namedMembers.Distinct(StringComparer.Ordinal).Count() == namedMembers.Length &&
                !namedMembers.Contains(member, StringComparer.Ordinal))
            {
                utterance = new SemanticUtterance(
                    UtteranceKind.Question,
                    new EnumerationRemainderQuestionFormula(
                        member,
                        group,
                        expectedCount,
                        missingPosition,
                        namedMembers),
                    source);
                return true;
            }
        }

        var tallestFeature = SemanticWorldModelQuestionPatterns.TallestFeaturePattern.Match(text);
        if (tallestFeature.Success)
        {
            var feature = SemanticWorldModelQuestionNormalization.Normalize(tallestFeature.Groups["feature"].Value);
            utterance = Create(
                SemanticWorldModelQuestionKind.HighestDataValue,
                subject: feature,
                concept: $"{feature}-elevation-feet",
                otherSubject: "feet above sea level",
                targetType: SemanticWorldModelQuestionNormalization.Normalize(tallestFeature.Groups["scope"].Value),
                sourceText: source);
            return true;
        }

        var maximumDataValue = SemanticWorldModelQuestionPatterns.MaximumDataValuePattern.Match(text);
        if (maximumDataValue.Success)
        {
            var metric = maximumDataValue.Groups["metric"].Value.Trim();
            utterance = Create(
                SemanticWorldModelQuestionKind.MaximumDataValue,
                subject: SemanticWorldModelQuestionNormalization.Normalize(maximumDataValue.Groups["subjectType"].Value),
                concept: SemanticWorldModelQuestionNormalization.NormalizeMaximumMetric(metric),
                otherSubject: metric,
                sourceText: source);
            return true;
        }

        var bestMoviesByDecade = SemanticWorldModelQuestionPatterns.BestMoviesByDecadePattern.Match(text);
        if (bestMoviesByDecade.Success)
        {
            var genre = SemanticWorldModelQuestionNormalization.Normalize(bestMoviesByDecade.Groups["genre"].Value);
            var decade = SemanticWorldModelQuestionNormalization.Normalize(bestMoviesByDecade.Groups["decade"].Value).Replace("'", string.Empty, StringComparison.Ordinal);
            utterance = Create(
                SemanticWorldModelQuestionKind.NotableExamples,
                subject: genre,
                concept: $"{genre}-film-{decade}",
                targetType: decade,
                limit: 12,
                sourceText: source);
            return true;
        }

        var typeInstanceList = SemanticWorldModelQuestionPatterns.TypeInstanceListPattern.Match(text);
        if (typeInstanceList.Success &&
            int.TryParse(typeInstanceList.Groups["count"].Value, out var instanceLimit))
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.TypeInstanceListQuery,
                subject: SemanticWorldModelQuestionNormalization.Normalize(typeInstanceList.Groups["type"].Value),
                targetType: instanceLimit.ToString(CultureInfo.InvariantCulture),
                sourceText: source);
            return true;
        }

        var listExamples = SemanticWorldModelQuestionPatterns.ListExamplesPattern.Match(text);
        if (listExamples.Success && SemanticWorldModelQuestionNormalization.TryParseCardinal(listExamples.Groups["count"].Value, out var listLimit))
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.ListExamples,
                subject: SemanticWorldModelQuestionNormalization.NormalizeListCategory(listExamples.Groups["category"].Value),
                limit: listLimit,
                sourceText: source);
            return true;
        }

        var creativeIdeas = SemanticWorldModelQuestionPatterns.CreativeIdeasPattern.Match(text);
        if (creativeIdeas.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.CreativeIdeas,
                subject: SemanticWorldModelQuestionNormalization.Normalize(creativeIdeas.Groups["item"].Value),
                sourceText: source);
            return true;
        }

        var beginnerGuidance = SemanticWorldModelQuestionPatterns.BeginnerGuidancePattern.Match(text);
        if (beginnerGuidance.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.BeginnerGuidance,
                subject: SemanticWorldModelQuestionNormalization.NormalizeBeginnerActivity(beginnerGuidance.Groups["activity"].Value),
                sourceText: source);
            return true;
        }

        var harmful = SemanticWorldModelQuestionPatterns.WhyHarmfulPattern.Match(text);
        if (harmful.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.WhyHarmful,
                subject: SemanticWorldModelQuestionNormalization.NormalizeHarmTerm(harmful.Groups["subject"].Value),
                concept: SemanticWorldModelQuestionNormalization.NormalizeHarmTerm(harmful.Groups["target"].Value),
                sourceText: source);
            return true;
        }

        var definition = SemanticWorldModelQuestionPatterns.DefinitionPattern.Match(text);
        if (definition.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.Definition,
                concept: SemanticWorldModelQuestionNormalization.Normalize(definition.Groups["concept"].Value),
                sourceText: source);
            return true;
        }

        var why = SemanticWorldModelQuestionPatterns.WhyPattern.Match(text);
        if (why.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.WhyClassification,
                subject: SemanticWorldModelQuestionNormalization.Normalize(why.Groups["subject"].Value),
                concept: SemanticWorldModelQuestionNormalization.Normalize(why.Groups["concept"].Value),
                sourceText: source);
            return true;
        }

        var capabilityExplanation = SemanticWorldModelQuestionPatterns.CapabilityExplanationPattern.Match(text);
        if (capabilityExplanation.Success &&
            string.Equals(capabilityExplanation.Groups["verb"].Value, "survive", StringComparison.OrdinalIgnoreCase) &&
            capabilityExplanation.Groups["context"].Value.Contains("water", StringComparison.OrdinalIgnoreCase))
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.CapabilityExplanation,
                subject: SemanticWorldModelQuestionNormalization.Normalize(capabilityExplanation.Groups["subject"].Value),
                concept: "survive",
                otherSubject: SemanticWorldModelQuestionNormalization.Normalize(capabilityExplanation.Groups["context"].Value),
                sourceText: source);
            return true;
        }

        var ability = SemanticWorldModelQuestionPatterns.AbilityPattern.Match(text);
        if (ability.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.Ability,
                subject: SemanticWorldModelQuestionNormalization.Normalize(ability.Groups["subject"].Value),
                concept: SemanticWorldModelQuestionNormalization.Normalize(ability.Groups["ability"].Value),
                sourceText: source);
            return true;
        }

        var mechanism = SemanticWorldModelQuestionPatterns.MechanismPattern.Match(text);
        if (mechanism.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.Mechanism,
                subject: SemanticWorldModelQuestionNormalization.Normalize(mechanism.Groups["subject"].Value),
                sourceText: source);
            return true;
        }

        var eventOutcomes = SemanticWorldModelQuestionPatterns.EventOutcomesPattern.Match(text);
        if (eventOutcomes.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.EventOutcomes,
                subject: SemanticWorldModelQuestionNormalization.Normalize(eventOutcomes.Groups["event"].Value),
                sourceText: source);
            return true;
        }

        var causes = SemanticWorldModelQuestionPatterns.CausesPattern.Match(text);
        if (causes.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.Causes,
                subject: SemanticWorldModelQuestionNormalization.Normalize(causes.Groups["subject"].Value),
                sourceText: source);
            return true;
        }

        var headquartersContribution = SemanticWorldModelQuestionPatterns.HeadquartersContributionPattern.Match(text);
        if (headquartersContribution.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.ContributionQuery,
                subject: SemanticWorldModelQuestionNormalization.NormalizeContributionTerm(headquartersContribution.Groups["recipient"].Value),
                concept: SemanticWorldModelQuestionNormalization.NormalizeContributionTerm(headquartersContribution.Groups["resource"].Value),
                otherSubject: SemanticWorldModelQuestionNormalization.NormalizeContributionTerm(headquartersContribution.Groups["location"].Value),
                targetType: SemanticWorldModelQuestionNormalization.NormalizeContributionTerm(headquartersContribution.Groups["facility"].Value),
                sourceText: source);
            return true;
        }

        var comparison = SemanticWorldModelQuestionPatterns.ComparePattern.Match(text);
        if (comparison.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.Compare,
                subject: SemanticWorldModelQuestionNormalization.Normalize(comparison.Groups["subject"].Value),
                otherSubject: SemanticWorldModelQuestionNormalization.Normalize(comparison.Groups["other"].Value),
                sourceText: source);
            return true;
        }

        if (SemanticWorldModelRelationQuestionParser.TryParse(source, text, out utterance))
        {
            return true;
        }

        return false;
    }

    private static bool TryParseScientificNumber(string value, out decimal number)
    {
        var normalized = Regex.Replace(
            value,
            @"\s*[×x]\s*10\^",
            "e",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return decimal.TryParse(
            normalized,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out number);
    }

    private static SemanticUtterance Create(
        SemanticWorldModelQuestionKind kind,
        string subject = "",
        string? concept = null,
        string? otherSubject = null,
        string? targetType = null,
        int limit = 0,
        string? sourceText = null) =>
        new(
            UtteranceKind.Question,
            new SemanticWorldModelQuestionFormula(kind, subject, concept, otherSubject, targetType, limit),
            sourceText ?? string.Empty);


}
