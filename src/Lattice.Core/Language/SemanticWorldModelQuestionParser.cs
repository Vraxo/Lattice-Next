using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class SemanticWorldModelQuestionParser
{
    private static readonly Regex ChargedParticleAccelerationPattern = new(
        @"^A (?<particle>[A-Za-z][A-Za-z -]*?) moving at (?:a speed of )?(?<speed>[0-9]+(?:\.[0-9]+)?(?:\s*(?:[×x]\s*10\^[+-]?[0-9]+|[eE][+-]?[0-9]+))?)\s*m/s is accelerated by a potential difference of (?<potential>[+\-−‐‑–—]?[0-9]+(?:\.[0-9]+)?)\s*V(?:\s*,?\s*what\s+is\s+its\s+final\s+(?:speed|velocity)\??|\s*\.?\s*Calculate\s+its\s+final\s+speed\.?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex SamplingParametersPattern = new(
        @"^What are [""“]?(?<first>[A-Za-z][A-Za-z -]*?)[""”]?\s+and\s+[""“]?(?<second>[A-Za-z][A-Za-z -]*?)(?:\s*\([^)]*\))?[""”]?\s+in\s+(?:a|the\s+)?(?<domain>[A-Za-z][A-Za-z -]*?)\??(?:\s+Can you provide an example of what the changes to these parameters would do\??)?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ProductionLocationReasonsPattern = new(
        @"^Why are (?:so )?many (?<activity>[A-Za-z][A-Za-z'’ -]*?) made in (?:the )?(?<location>[A-Za-z][A-Za-z'’ -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ContextualRelationRolePattern = new(
        @"^What is the name of (?:the )?(?<possessor>[A-Za-z][A-Za-z'’ -]*?)'s (?<role>[A-Za-z][A-Za-z -]*?) on (?:the )?(?:TV show )?[""“]?(?<context>[A-Za-z][A-Za-z0-9'’ -]*?)[""”]?\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex MissingListItemsPattern = new(
        @"^Tell me whether or not these (?<items>[A-Za-z][A-Za-z -]*?) are on (?<list>[A-Za-z][A-Za-z'’ -]*?)(?: list)?[?.]?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex CommonIngredientsPattern = new(
        @"^What are common ingredients of (?:a|the) (?<dish>[A-Za-z][A-Za-z -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex RegionalNamePattern = new(
        @"^In (?<domain>[A-Za-z][A-Za-z -]*?) what do the (?<region>[A-Za-z][A-Za-z -]*?) call an? (?<subject>[A-Za-z][A-Za-z -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex DifferenceBetweenPattern = new(
        @"^What is the difference between (?<subject>[A-Za-z][A-Za-z -]*?) and (?<other>[A-Za-z][A-Za-z -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex RequiredKnowledgePattern = new(
        @"^How much (?<knowledge>[A-Za-z][A-Za-z -]*?) is required to (?<activity>[A-Za-z][A-Za-z -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex EverydayApplicationsPattern = new(
        @"^How can (?<subject>[A-Za-z][A-Za-z'’ -]*?) be used in (?:day to day|everyday) life\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex WhyShouldUsePattern = new(
        @"^Why should (?:I|we) use (?<subject>[A-Za-z][A-Za-z -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex SimilarChatbotsPattern = new(
        @"^(?:And )?(?:can you give me )?some examples of advanced chatbots that are similar to (?<subject>[A-Za-z][A-Za-z -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex CompanyPurposeQuestionPattern = new(
        @"^I\s+(?:started|founded|launched)\s+(?:a\s+)?(?:new\s+)?company\s+named\s+(?<company>[A-Z][A-Za-z0-9&'’ -]*?(?:\s+(?:Inc|Incorporated|Ltd|Limited|LLC|LLP|Corp|Corporation))?)\.\s*What\s+(?:do\s+we|does\s+(?:it|the\s+company))\s+do\?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex EnumerationRemainderPattern = new(
        @"^(?<member>[A-Z][A-Za-z'-]*)['’]s parents have (?<count>one|two|three|four|five|six|seven|eight|nine|ten|[0-9]+) (?<group>[A-Za-z][A-Za-z'-]*): (?<known>[A-Z][A-Za-z'-]*(?:, [A-Z][A-Za-z'-]*)*), and what(?:'s|’s| is) the name of the (?<ordinal>first|second|third|fourth|fifth|sixth|seventh|eighth|ninth|tenth|[0-9]+(?:st|nd|rd|th)) (?<questionGroup>[A-Za-z][A-Za-z'-]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex DefinitionPattern = new(
        @"^What makes something an? (?<concept>[A-Za-z][A-Za-z -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex WhyPattern = new(
        @"^Why is (?<subject>[A-Za-z][A-Za-z -]*?) an? (?<concept>[A-Za-z][A-Za-z -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex AbilityPattern = new(
        @"^Why can (?<subject>[A-Za-z][A-Za-z -]*?) (?<ability>[A-Za-z][A-Za-z -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex CapabilityExplanationPattern = new(
        @"^Why can (?<subject>[A-Za-z][A-Za-z0-9'’ -]*?) (?<verb>[A-Za-z]+)(?<context>.*?)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex MechanismPattern = new(
        @"^How does (?<subject>[A-Za-z][A-Za-z -]*?) produce sound\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex TallestFeaturePattern = new(
        @"^What is the tallest (?<feature>[A-Za-z][A-Za-z -]*?) in the (?<scope>[A-Za-z][A-Za-z -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex MaximumDataValuePattern = new(
        @"^What (?<subjectType>individual|person|athlete) has won the most (?<metric>[A-Za-z][A-Za-z -]*?)(?: in the history of (?:the )?(?<scope>games|Olympic games))?[?.]?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex BestMoviesByDecadePattern = new(
        @"^What are some of the best (?<genre>[A-Za-z][A-Za-z -]*?) movies of the (?<decade>[0-9]{4}'?s)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex TypeInstanceListPattern = new(
        @"^Give me the top (?<count>[0-9]+) (?<type>[A-Za-z][A-Za-z -]*?) names?\.?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ListExamplesPattern = new(
        @"^Give me (?:the )?(?:top )?(?<count>five|four|three|two|one|[0-9]+) (?<category>[A-Za-z][A-Za-z -]*?) names(?: in a numbered list)?(?:\. Names only; no descriptions)?[?.]?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex CreativeIdeasPattern = new(
        @"^What are some (?<style>[A-Za-z][A-Za-z -]*?) (?<item>[A-Za-z][A-Za-z -]*?) that you can make yourself\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex BeginnerGuidancePattern = new(
        @"^How do I start (?<activity>[A-Za-z][A-Za-z -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex WhyHarmfulPattern = new(
        @"^Why (?:(?:is|are) (?<subject>[A-Za-z][A-Za-z -]*?) bad for|(?<subject>[A-Za-z][A-Za-z -]*?) (?:is|are)? bad for) (?<target>[A-Za-z][A-Za-z -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex EventOutcomesPattern = new(
        @"^What happens when (?<event>[A-Za-z][A-Za-z -]*?)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex CausesPattern = new(
        @"^What causes (?<subject>[A-Za-z][A-Za-z -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex HeadquartersContributionPattern = new(
        @"^Who gave (?:the )?(?<recipient>[A-Za-z][A-Za-z0-9'’ -]*?) the (?<resource>[A-Za-z][A-Za-z -]*?) in (?<location>[A-Za-z][A-Za-z0-9'’ -]*?) to build (?:(?:their|its|the) )?(?<facility>[A-Za-z][A-Za-z -]*?)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ComparePattern = new(
        @"^What distinguishes (?<subject>[A-Za-z][A-Za-z -]*?) from (?<other>[A-Za-z][A-Za-z -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static bool TryParse(string source, out SemanticUtterance? utterance)
    {
        utterance = null;
        var text = source.Trim();

        if (SemanticWorldModelRelationQuestionParser.TryParsePair(source, text, out utterance))
        {
            return true;
        }

        var chargedParticleAcceleration = ChargedParticleAccelerationPattern.Match(text);
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

        var samplingParameters = SamplingParametersPattern.Match(text);
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

        var productionLocationReasons = ProductionLocationReasonsPattern.Match(text);
        if (productionLocationReasons.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.ProductionLocationReasons,
                subject: SemanticWorldModelQuestionNormalization.Normalize(productionLocationReasons.Groups["activity"].Value),
                otherSubject: SemanticWorldModelQuestionNormalization.Normalize(productionLocationReasons.Groups["location"].Value),
                sourceText: source);
            return true;
        }

        var contextualRole = ContextualRelationRolePattern.Match(text);
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

        var commonIngredients = CommonIngredientsPattern.Match(text);
        if (commonIngredients.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.Ingredients,
                subject: SemanticWorldModelQuestionNormalization.Normalize(commonIngredients.Groups["dish"].Value),
                sourceText: source);
            return true;
        }

        var regionalName = RegionalNamePattern.Match(text);
        if (regionalName.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.RegionalName,
                subject: SemanticWorldModelQuestionNormalization.Normalize(regionalName.Groups["subject"].Value),
                targetType: SemanticWorldModelQuestionNormalization.NormalizeRegionalVariant(regionalName.Groups["region"].Value),
                sourceText: source);
            return true;
        }

        var missingListItems = MissingListItemsPattern.Match(text);
        if (missingListItems.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.Clarification,
                subject: SemanticWorldModelQuestionNormalization.Normalize(missingListItems.Groups["items"].Value),
                otherSubject: $"{missingListItems.Groups["list"].Value.Trim()} list",
                sourceText: source);
            return true;
        }

        var companyPurpose = CompanyPurposeQuestionPattern.Match(text);
        if (companyPurpose.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.CreativeIdeas,
                subject: "business-ideas",
                otherSubject: companyPurpose.Groups["company"].Value.Trim(),
                sourceText: source);
            return true;
        }

        var requiredKnowledge = RequiredKnowledgePattern.Match(text);
        if (requiredKnowledge.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.RequiredKnowledge,
                subject: SemanticWorldModelQuestionNormalization.Normalize(requiredKnowledge.Groups["activity"].Value),
                concept: SemanticWorldModelQuestionNormalization.Normalize(requiredKnowledge.Groups["knowledge"].Value),
                sourceText: source);
            return true;
        }

        var everydayApplications = EverydayApplicationsPattern.Match(text);
        if (everydayApplications.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.Applications,
                subject: SemanticWorldModelQuestionNormalization.Normalize(everydayApplications.Groups["subject"].Value),
                sourceText: source);
            return true;
        }

        var difference = DifferenceBetweenPattern.Match(text);
        if (difference.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.Compare,
                subject: SemanticWorldModelQuestionNormalization.Normalize(difference.Groups["subject"].Value),
                otherSubject: SemanticWorldModelQuestionNormalization.Normalize(difference.Groups["other"].Value),
                sourceText: source);
            return true;
        }

        var whyShouldUse = WhyShouldUsePattern.Match(text);
        if (whyShouldUse.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.WhyUse,
                subject: SemanticWorldModelQuestionNormalization.Normalize(whyShouldUse.Groups["subject"].Value),
                sourceText: source);
            return true;
        }

        var similarChatbots = SimilarChatbotsPattern.Match(text);
        if (similarChatbots.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.ListExamples,
                subject: "advanced-chatbot",
                limit: 5,
                sourceText: source);
            return true;
        }

        var enumerationRemainder = EnumerationRemainderPattern.Match(text);
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

        var tallestFeature = TallestFeaturePattern.Match(text);
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

        var maximumDataValue = MaximumDataValuePattern.Match(text);
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

        var bestMoviesByDecade = BestMoviesByDecadePattern.Match(text);
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

        var typeInstanceList = TypeInstanceListPattern.Match(text);
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

        var listExamples = ListExamplesPattern.Match(text);
        if (listExamples.Success && SemanticWorldModelQuestionNormalization.TryParseCardinal(listExamples.Groups["count"].Value, out var listLimit))
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.ListExamples,
                subject: SemanticWorldModelQuestionNormalization.NormalizeListCategory(listExamples.Groups["category"].Value),
                limit: listLimit,
                sourceText: source);
            return true;
        }

        var creativeIdeas = CreativeIdeasPattern.Match(text);
        if (creativeIdeas.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.CreativeIdeas,
                subject: SemanticWorldModelQuestionNormalization.Normalize(creativeIdeas.Groups["item"].Value),
                sourceText: source);
            return true;
        }

        var beginnerGuidance = BeginnerGuidancePattern.Match(text);
        if (beginnerGuidance.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.BeginnerGuidance,
                subject: SemanticWorldModelQuestionNormalization.NormalizeBeginnerActivity(beginnerGuidance.Groups["activity"].Value),
                sourceText: source);
            return true;
        }

        var harmful = WhyHarmfulPattern.Match(text);
        if (harmful.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.WhyHarmful,
                subject: SemanticWorldModelQuestionNormalization.NormalizeHarmTerm(harmful.Groups["subject"].Value),
                concept: SemanticWorldModelQuestionNormalization.NormalizeHarmTerm(harmful.Groups["target"].Value),
                sourceText: source);
            return true;
        }

        var definition = DefinitionPattern.Match(text);
        if (definition.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.Definition,
                concept: SemanticWorldModelQuestionNormalization.Normalize(definition.Groups["concept"].Value),
                sourceText: source);
            return true;
        }

        var why = WhyPattern.Match(text);
        if (why.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.WhyClassification,
                subject: SemanticWorldModelQuestionNormalization.Normalize(why.Groups["subject"].Value),
                concept: SemanticWorldModelQuestionNormalization.Normalize(why.Groups["concept"].Value),
                sourceText: source);
            return true;
        }

        var capabilityExplanation = CapabilityExplanationPattern.Match(text);
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

        var ability = AbilityPattern.Match(text);
        if (ability.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.Ability,
                subject: SemanticWorldModelQuestionNormalization.Normalize(ability.Groups["subject"].Value),
                concept: SemanticWorldModelQuestionNormalization.Normalize(ability.Groups["ability"].Value),
                sourceText: source);
            return true;
        }

        var mechanism = MechanismPattern.Match(text);
        if (mechanism.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.Mechanism,
                subject: SemanticWorldModelQuestionNormalization.Normalize(mechanism.Groups["subject"].Value),
                sourceText: source);
            return true;
        }

        var eventOutcomes = EventOutcomesPattern.Match(text);
        if (eventOutcomes.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.EventOutcomes,
                subject: SemanticWorldModelQuestionNormalization.Normalize(eventOutcomes.Groups["event"].Value),
                sourceText: source);
            return true;
        }

        var causes = CausesPattern.Match(text);
        if (causes.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.Causes,
                subject: SemanticWorldModelQuestionNormalization.Normalize(causes.Groups["subject"].Value),
                sourceText: source);
            return true;
        }

        var headquartersContribution = HeadquartersContributionPattern.Match(text);
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

        var comparison = ComparePattern.Match(text);
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
