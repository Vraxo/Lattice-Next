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

    private static readonly Regex RelationTargetPattern = new(
        @"^What does (?<subject>[A-Za-z][A-Za-z -]*?) (?<verb>[a-z]+)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ScopedRelationTargetPattern = new(
        @"^Which (?<target>[A-Za-z][A-Za-z -]*?) of (?<scope>[A-Za-z0-9][A-Za-z0-9'’ -]*?) did (?<subject>[A-Za-z][A-Za-z'’ -]*?) (?<verb>[a-z]+)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ScopedTypedRelationTargetPattern = new(
        @"^Which (?<targetType>[A-Za-z][A-Za-z -]*?) of (?<scope>[A-Za-z0-9][A-Za-z0-9'’ -]*?) did (?<subject>[A-Za-z][A-Za-z'’ -]*?) (?<verb>[a-z]+)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex TypedRelationTargetPattern = new(
        @"^Which (?<targetType>[A-Za-z][A-Za-z -]*?) did (?<subject>[A-Za-z][A-Za-z'’ -]*?) (?<verb>[a-z]+)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex TypedRelationSourcePattern = new(
        @"^Which (?<subjectType>(?!(?:is|are|was|were|can|do|does|did)\b)[A-Za-z][A-Za-z -]*?) (?<verb>[a-z]+) (?<object>[""“]?[A-Z][A-Za-z0-9'’ -]*?[""”]?)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex RelationSourcePattern = new(
        @"^(?:Who|What) (?!(?:is|are|was|were|be|being|been|has|have|does|do|did)\b)(?<verb>[a-z]+s) (?<object>(?:(?:the|a|an) )?(?!(?:when|where|why|how|to|for|from|with|by|in|on|at|of|that|which|who|what)\b)[A-Za-z]+(?: [A-Za-z]+){0,2})\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static bool TryParse(string source, out SemanticUtterance? utterance)
    {
        utterance = null;
        var text = source.Trim();

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
                    Normalize(chargedParticleAcceleration.Groups["particle"].Value),
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
                subject: Normalize(samplingParameters.Groups["first"].Value),
                otherSubject: Normalize(samplingParameters.Groups["second"].Value),
                targetType: Normalize(samplingParameters.Groups["domain"].Value),
                sourceText: source);
            return true;
        }

        var productionLocationReasons = ProductionLocationReasonsPattern.Match(text);
        if (productionLocationReasons.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.ProductionLocationReasons,
                subject: Normalize(productionLocationReasons.Groups["activity"].Value),
                otherSubject: Normalize(productionLocationReasons.Groups["location"].Value),
                sourceText: source);
            return true;
        }

        var contextualRole = ContextualRelationRolePattern.Match(text);
        if (contextualRole.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.ContextualRelationRoleQuery,
                subject: Normalize(contextualRole.Groups["possessor"].Value),
                concept: Normalize(contextualRole.Groups["role"].Value),
                otherSubject: Normalize(contextualRole.Groups["context"].Value),
                sourceText: source);
            return true;
        }

        var commonIngredients = CommonIngredientsPattern.Match(text);
        if (commonIngredients.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.Ingredients,
                subject: Normalize(commonIngredients.Groups["dish"].Value),
                sourceText: source);
            return true;
        }

        var regionalName = RegionalNamePattern.Match(text);
        if (regionalName.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.RegionalName,
                subject: Normalize(regionalName.Groups["subject"].Value),
                targetType: NormalizeRegionalVariant(regionalName.Groups["region"].Value),
                sourceText: source);
            return true;
        }

        var missingListItems = MissingListItemsPattern.Match(text);
        if (missingListItems.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.Clarification,
                subject: Normalize(missingListItems.Groups["items"].Value),
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
                subject: Normalize(requiredKnowledge.Groups["activity"].Value),
                concept: Normalize(requiredKnowledge.Groups["knowledge"].Value),
                sourceText: source);
            return true;
        }

        var everydayApplications = EverydayApplicationsPattern.Match(text);
        if (everydayApplications.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.Applications,
                subject: Normalize(everydayApplications.Groups["subject"].Value),
                sourceText: source);
            return true;
        }

        var difference = DifferenceBetweenPattern.Match(text);
        if (difference.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.Compare,
                subject: Normalize(difference.Groups["subject"].Value),
                otherSubject: Normalize(difference.Groups["other"].Value),
                sourceText: source);
            return true;
        }

        var whyShouldUse = WhyShouldUsePattern.Match(text);
        if (whyShouldUse.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.WhyUse,
                subject: Normalize(whyShouldUse.Groups["subject"].Value),
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
            TryParseCardinal(enumerationRemainder.Groups["count"].Value, out var expectedCount) &&
            TryParseOrdinal(enumerationRemainder.Groups["ordinal"].Value, out var missingPosition))
        {
            var namedMembers = enumerationRemainder.Groups["known"].Value
                .Split(", ", StringSplitOptions.RemoveEmptyEntries)
                .Select(Normalize)
                .ToImmutableArray();
            var member = Normalize(enumerationRemainder.Groups["member"].Value);
            var group = Normalize(enumerationRemainder.Groups["group"].Value);
            var questionGroup = Normalize(enumerationRemainder.Groups["questionGroup"].Value);

            if (expectedCount > 0 &&
                missingPosition == expectedCount &&
                SingularizeEnumerationGroup(group) == SingularizeEnumerationGroup(questionGroup) &&
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
            var feature = Normalize(tallestFeature.Groups["feature"].Value);
            utterance = Create(
                SemanticWorldModelQuestionKind.HighestDataValue,
                subject: feature,
                concept: $"{feature}-elevation-feet",
                otherSubject: "feet above sea level",
                targetType: Normalize(tallestFeature.Groups["scope"].Value),
                sourceText: source);
            return true;
        }

        var maximumDataValue = MaximumDataValuePattern.Match(text);
        if (maximumDataValue.Success)
        {
            var metric = maximumDataValue.Groups["metric"].Value.Trim();
            utterance = Create(
                SemanticWorldModelQuestionKind.MaximumDataValue,
                subject: Normalize(maximumDataValue.Groups["subjectType"].Value),
                concept: NormalizeMaximumMetric(metric),
                otherSubject: metric,
                sourceText: source);
            return true;
        }

        var bestMoviesByDecade = BestMoviesByDecadePattern.Match(text);
        if (bestMoviesByDecade.Success)
        {
            var genre = Normalize(bestMoviesByDecade.Groups["genre"].Value);
            var decade = Normalize(bestMoviesByDecade.Groups["decade"].Value).Replace("'", string.Empty, StringComparison.Ordinal);
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
                subject: Normalize(typeInstanceList.Groups["type"].Value),
                targetType: instanceLimit.ToString(CultureInfo.InvariantCulture),
                sourceText: source);
            return true;
        }

        var listExamples = ListExamplesPattern.Match(text);
        if (listExamples.Success && TryParseCardinal(listExamples.Groups["count"].Value, out var listLimit))
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.ListExamples,
                subject: NormalizeListCategory(listExamples.Groups["category"].Value),
                limit: listLimit,
                sourceText: source);
            return true;
        }

        var creativeIdeas = CreativeIdeasPattern.Match(text);
        if (creativeIdeas.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.CreativeIdeas,
                subject: Normalize(creativeIdeas.Groups["item"].Value),
                sourceText: source);
            return true;
        }

        var beginnerGuidance = BeginnerGuidancePattern.Match(text);
        if (beginnerGuidance.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.BeginnerGuidance,
                subject: NormalizeBeginnerActivity(beginnerGuidance.Groups["activity"].Value),
                sourceText: source);
            return true;
        }

        var harmful = WhyHarmfulPattern.Match(text);
        if (harmful.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.WhyHarmful,
                subject: NormalizeHarmTerm(harmful.Groups["subject"].Value),
                concept: NormalizeHarmTerm(harmful.Groups["target"].Value),
                sourceText: source);
            return true;
        }

        var definition = DefinitionPattern.Match(text);
        if (definition.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.Definition,
                concept: Normalize(definition.Groups["concept"].Value),
                sourceText: source);
            return true;
        }

        var why = WhyPattern.Match(text);
        if (why.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.WhyClassification,
                subject: Normalize(why.Groups["subject"].Value),
                concept: Normalize(why.Groups["concept"].Value),
                sourceText: source);
            return true;
        }

        var capabilityExplanation = CapabilityExplanationPattern.Match(text);
        if (capabilityExplanation.Success &&
            string.Equals(capabilityExplanation.Groups["verb"].Value, "survive", StringComparison.OrdinalIgnoreCase))
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.CapabilityExplanation,
                subject: Normalize(capabilityExplanation.Groups["subject"].Value),
                concept: "survive",
                otherSubject: Normalize(capabilityExplanation.Groups["context"].Value),
                sourceText: source);
            return true;
        }

        var ability = AbilityPattern.Match(text);
        if (ability.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.Ability,
                subject: Normalize(ability.Groups["subject"].Value),
                concept: Normalize(ability.Groups["ability"].Value),
                sourceText: source);
            return true;
        }

        var mechanism = MechanismPattern.Match(text);
        if (mechanism.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.Mechanism,
                subject: Normalize(mechanism.Groups["subject"].Value),
                sourceText: source);
            return true;
        }

        var eventOutcomes = EventOutcomesPattern.Match(text);
        if (eventOutcomes.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.EventOutcomes,
                subject: Normalize(eventOutcomes.Groups["event"].Value),
                sourceText: source);
            return true;
        }

        var causes = CausesPattern.Match(text);
        if (causes.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.Causes,
                subject: Normalize(causes.Groups["subject"].Value),
                sourceText: source);
            return true;
        }

        var headquartersContribution = HeadquartersContributionPattern.Match(text);
        if (headquartersContribution.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.ContributionQuery,
                subject: NormalizeContributionTerm(headquartersContribution.Groups["recipient"].Value),
                concept: NormalizeContributionTerm(headquartersContribution.Groups["resource"].Value),
                otherSubject: NormalizeContributionTerm(headquartersContribution.Groups["location"].Value),
                targetType: NormalizeContributionTerm(headquartersContribution.Groups["facility"].Value),
                sourceText: source);
            return true;
        }

        var comparison = ComparePattern.Match(text);
        if (comparison.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.Compare,
                subject: Normalize(comparison.Groups["subject"].Value),
                otherSubject: Normalize(comparison.Groups["other"].Value),
                sourceText: source);
            return true;
        }

        var scopedTypedRelationTarget = ScopedTypedRelationTargetPattern.Match(text);
        if (scopedTypedRelationTarget.Success)
        {
            utterance = new SemanticUtterance(
                UtteranceKind.Question,
                new SemanticRelationQueryFormula(
                    Normalize(scopedTypedRelationTarget.Groups["subject"].Value),
                    Normalize(scopedTypedRelationTarget.Groups["verb"].Value),
                    Normalize(scopedTypedRelationTarget.Groups["scope"].Value),
                    NormalizeType(scopedTypedRelationTarget.Groups["targetType"].Value)),
                source);
            return true;
        }

        var typedRelationTarget = TypedRelationTargetPattern.Match(text);
        if (typedRelationTarget.Success)
        {
            utterance = new SemanticUtterance(
                UtteranceKind.Question,
                new SemanticRelationQueryFormula(
                    Normalize(typedRelationTarget.Groups["subject"].Value),
                    Normalize(typedRelationTarget.Groups["verb"].Value),
                    TargetType: NormalizeType(typedRelationTarget.Groups["targetType"].Value)),
                source);
            return true;
        }

        var scopedRelationTarget = ScopedRelationTargetPattern.Match(text);
        if (scopedRelationTarget.Success)
        {
            utterance = new SemanticUtterance(
                UtteranceKind.Question,
                new SemanticRelationQueryFormula(
                    Normalize(scopedRelationTarget.Groups["subject"].Value),
                    Normalize(scopedRelationTarget.Groups["verb"].Value),
                    Normalize(scopedRelationTarget.Groups["scope"].Value)),
                source);
            return true;
        }

        var relationTarget = RelationTargetPattern.Match(text);
        if (relationTarget.Success)
        {
            utterance = new SemanticUtterance(
                UtteranceKind.Question,
                new SemanticRelationQueryFormula(
                    Normalize(relationTarget.Groups["subject"].Value),
                    Normalize(relationTarget.Groups["verb"].Value)),
                source);
            return true;
        }

        var typedRelationSource = TypedRelationSourcePattern.Match(text);
        if (typedRelationSource.Success)
        {
            utterance = new SemanticUtterance(
                UtteranceKind.Question,
                new SemanticRelationSourceQueryFormula(
                    NormalizeQuotedTerm(typedRelationSource.Groups["object"].Value),
                    Normalize(typedRelationSource.Groups["verb"].Value),
                    Normalize(typedRelationSource.Groups["subjectType"].Value)),
                source);
            return true;
        }

        var relationSource = RelationSourcePattern.Match(text);
        if (relationSource.Success)
        {
            utterance = new SemanticUtterance(
                UtteranceKind.Question,
                new SemanticRelationSourceQueryFormula(
                    Normalize(relationSource.Groups["object"].Value),
                    Normalize(relationSource.Groups["verb"].Value)),
                source);
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

    private static string SingularizeEnumerationGroup(string group) =>
        group switch
        {
            "children" => "child",
            "people" => "person",
            "men" => "man",
            "women" => "woman",
            "mice" => "mouse",
            "geese" => "goose",
            "teeth" => "tooth",
            "feet" => "foot",
            _ when group.EndsWith("ies", StringComparison.Ordinal) => $"{group[..^3]}y",
            _ when group.EndsWith("sses", StringComparison.Ordinal) ||
                   group.EndsWith("shes", StringComparison.Ordinal) ||
                   group.EndsWith("ches", StringComparison.Ordinal) ||
                   group.EndsWith("xes", StringComparison.Ordinal) ||
                   group.EndsWith("zes", StringComparison.Ordinal) => group[..^2],
            _ when group.EndsWith("s", StringComparison.Ordinal) &&
                   !group.EndsWith("ss", StringComparison.Ordinal) => group[..^1],
            _ => group
        };

    private static bool TryParseCardinal(string value, out int number)
    {
        if (int.TryParse(value, out number))
        {
            return number > 0;
        }

        number = value.ToLowerInvariant() switch
        {
            "one" => 1,
            "two" => 2,
            "three" => 3,
            "four" => 4,
            "five" => 5,
            "six" => 6,
            "seven" => 7,
            "eight" => 8,
            "nine" => 9,
            "ten" => 10,
            _ => 0
        };

        return number > 0;
    }

    private static bool TryParseOrdinal(string value, out int number)
    {
        number = value.ToLowerInvariant() switch
        {
            "first" => 1,
            "second" => 2,
            "third" => 3,
            "fourth" => 4,
            "fifth" => 5,
            "sixth" => 6,
            "seventh" => 7,
            "eighth" => 8,
            "ninth" => 9,
            "tenth" => 10,
            _ => 0
        };

        if (number > 0)
        {
            return true;
        }

        var digits = Regex.Match(value, @"^(?<number>[0-9]+)(?:st|nd|rd|th)$");
        return digits.Success && int.TryParse(digits.Groups["number"].Value, out number) && number > 0;
    }

    private static string NormalizeMaximumMetric(string value) =>
        Normalize(value) switch
        {
            "olympic-gold-medal" or "olympic-gold-medals" => "olympic-gold-medal-count",
            _ => Normalize(value)
        };

    private static string NormalizeListCategory(string value) =>
        Normalize(value) switch
        {
            "golf-equipment-companies" or "golf-equipment-company-names" => "golf-equipment-company",
            "golf-equipment-company" => "golf-equipment-company",
            _ => Normalize(value)
        };

    private static string NormalizeBeginnerActivity(string value) =>
        Normalize(value) switch
        {
            "run" or "running" or "jogging" => "running",
            _ => Normalize(value)
        };

    private static string NormalizeHarmTerm(string value) =>
        Normalize(value) switch
        {
            "mobile" or "mobile-phone" or "mobile-phones" or
            "smartphone" or "smartphones" or "smartphone-use" or "phone-use" or
            "cell-phone" or "cell-phones" => "mobile-phone-use",
            "human" or "humans" or "people" => "person",
            _ => Normalize(value)
        };

    private static string NormalizeContributionTerm(string value) =>
        Normalize(value) switch
        {
            "un" => "united-nations",
            "ny" => "new-york-city",
            "land" => "land-parcel",
            "hq" or "headquarters" => "headquarters-site",
            _ => Normalize(value)
        };

    private static string NormalizeQuotedTerm(string value) =>
        Normalize(value.Trim().Trim('"', '\u201c', '\u201d', '\u0027', '\u2019'));

    private static string NormalizeRegionalVariant(string value) =>
        Normalize(value) switch
        {
            "american" or "americans" or "united-states" => "american-english",
            "british" or "brits" or "united-kingdom" => "british-english",
            _ => Normalize(value)
        };

    private static string NormalizeType(string value)
    {
        var normalized = Normalize(value);
        if (normalized.EndsWith("ies", StringComparison.Ordinal) && normalized.Length > 3)
        {
            return normalized[..^3] + "y";
        }

        return normalized.EndsWith('s') && !normalized.EndsWith("ss", StringComparison.Ordinal)
            ? normalized[..^1]
            : normalized;
    }

    private static string Normalize(string value)
    {
        var normalized = value.Trim();
        foreach (var article in new[] { "a ", "an ", "the " })
        {
            if (normalized.StartsWith(article, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[article.Length..];
                break;
            }
        }

        return SemanticWorldModel.Normalize(normalized);
    }
}
