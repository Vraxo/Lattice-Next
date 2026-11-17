using System.Text.RegularExpressions;

namespace Lattice.Core.Language;

internal static class SemanticWorldModelQuestionPatterns
{
    internal static readonly Regex ChargedParticleAccelerationPattern = new(
        @"^A (?<particle>[A-Za-z][A-Za-z -]*?) moving at (?:a speed of )?(?<speed>[0-9]+(?:\.[0-9]+)?(?:\s*(?:[×x]\s*10\^[+-]?[0-9]+|[eE][+-]?[0-9]+))?)\s*m/s is accelerated by a potential difference of (?<potential>[+\-−‐‑–—]?[0-9]+(?:\.[0-9]+)?)\s*V(?:\s*,?\s*what\s+is\s+its\s+final\s+(?:speed|velocity)\??|\s*\.?\s*Calculate\s+its\s+final\s+speed\.?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static readonly Regex SamplingParametersPattern = new(
        @"^What are [""“]?(?<first>[A-Za-z][A-Za-z -]*?)[""”]?\s+and\s+[""“]?(?<second>[A-Za-z][A-Za-z -]*?)(?:\s*\([^)]*\))?[""”]?\s+in\s+(?:a|the\s+)?(?<domain>[A-Za-z][A-Za-z -]*?)\??(?:\s+Can you provide an example of what the changes to these parameters would do\??)?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static readonly Regex ProductionLocationReasonsPattern = new(
        @"^Why are (?:so )?many (?<activity>[A-Za-z][A-Za-z'’ -]*?) made in (?:the )?(?<location>[A-Za-z][A-Za-z'’ -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static readonly Regex ContextualRelationRolePattern = new(
        @"^What is the name of (?:the )?(?<possessor>[A-Za-z][A-Za-z'’ -]*?)'s (?<role>[A-Za-z][A-Za-z -]*?) on (?:the )?(?:TV show )?[""“]?(?<context>[A-Za-z][A-Za-z0-9'’ -]*?)[""”]?\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static readonly Regex MissingListItemsPattern = new(
        @"^Tell me whether or not these (?<items>[A-Za-z][A-Za-z -]*?) are on (?<list>[A-Za-z][A-Za-z'’ -]*?)(?: list)?[?.]?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static readonly Regex CommonIngredientsPattern = new(
        @"^What are common ingredients of (?:a|the) (?<dish>[A-Za-z][A-Za-z -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static readonly Regex RegionalNamePattern = new(
        @"^In (?<domain>[A-Za-z][A-Za-z -]*?) what do the (?<region>[A-Za-z][A-Za-z -]*?) call an? (?<subject>[A-Za-z][A-Za-z -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static readonly Regex DifferenceBetweenPattern = new(
        @"^What is the difference between (?<subject>[A-Za-z][A-Za-z -]*?) and (?<other>[A-Za-z][A-Za-z -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static readonly Regex RequiredKnowledgePattern = new(
        @"^How much (?<knowledge>[A-Za-z][A-Za-z -]*?) is required to (?<activity>[A-Za-z][A-Za-z -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static readonly Regex EverydayApplicationsPattern = new(
        @"^How can (?<subject>[A-Za-z][A-Za-z'’ -]*?) be used in (?:day to day|everyday) life\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static readonly Regex WhyShouldUsePattern = new(
        @"^Why should (?:I|we) use (?<subject>[A-Za-z][A-Za-z -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static readonly Regex SimilarChatbotsPattern = new(
        @"^(?:And )?(?:can you give me )?some examples of advanced chatbots that are similar to (?<subject>[A-Za-z][A-Za-z -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static readonly Regex CompanyPurposeQuestionPattern = new(
        @"^I\s+(?:started|founded|launched)\s+(?:a\s+)?(?:new\s+)?company\s+named\s+(?<company>[A-Z][A-Za-z0-9&'’ -]*?(?:\s+(?:Inc|Incorporated|Ltd|Limited|LLC|LLP|Corp|Corporation))?)\.\s*What\s+(?:do\s+we|does\s+(?:it|the\s+company))\s+do\?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static readonly Regex EnumerationRemainderPattern = new(
        @"^(?<member>[A-Z][A-Za-z'-]*)['’]s parents have (?<count>one|two|three|four|five|six|seven|eight|nine|ten|[0-9]+) (?<group>[A-Za-z][A-Za-z'-]*): (?<known>[A-Z][A-Za-z'-]*(?:, [A-Z][A-Za-z'-]*)*), and what(?:'s|’s| is) the name of the (?<ordinal>first|second|third|fourth|fifth|sixth|seventh|eighth|ninth|tenth|[0-9]+(?:st|nd|rd|th)) (?<questionGroup>[A-Za-z][A-Za-z'-]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static readonly Regex DefinitionPattern = new(
        @"^What makes something an? (?<concept>[A-Za-z][A-Za-z -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static readonly Regex WhyPattern = new(
        @"^Why is (?<subject>[A-Za-z][A-Za-z -]*?) an? (?<concept>[A-Za-z][A-Za-z -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static readonly Regex AbilityPattern = new(
        @"^Why can (?<subject>[A-Za-z][A-Za-z -]*?) (?<ability>[A-Za-z][A-Za-z -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static readonly Regex CapabilityExplanationPattern = new(
        @"^Why can (?<subject>[A-Za-z][A-Za-z0-9'’ -]*?) (?<verb>[A-Za-z]+)(?<context>.*?)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static readonly Regex MechanismPattern = new(
        @"^How does (?<subject>[A-Za-z][A-Za-z -]*?) produce sound\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static readonly Regex TallestFeaturePattern = new(
        @"^What is the tallest (?<feature>[A-Za-z][A-Za-z -]*?) in the (?<scope>[A-Za-z][A-Za-z -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static readonly Regex MaximumDataValuePattern = new(
        @"^What (?<subjectType>individual|person|athlete) has won the most (?<metric>[A-Za-z][A-Za-z -]*?)(?: in the history of (?:the )?(?<scope>games|Olympic games))?[?.]?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static readonly Regex BestMoviesByDecadePattern = new(
        @"^What are some of the best (?<genre>[A-Za-z][A-Za-z -]*?) movies of the (?<decade>[0-9]{4}'?s)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static readonly Regex TypeInstanceListPattern = new(
        @"^Give me the top (?<count>[0-9]+) (?<type>[A-Za-z][A-Za-z -]*?) names?\.?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static readonly Regex ListExamplesPattern = new(
        @"^Give me (?:the )?(?:top )?(?<count>five|four|three|two|one|[0-9]+) (?<category>[A-Za-z][A-Za-z -]*?) names(?: in a numbered list)?(?:\. Names only; no descriptions)?[?.]?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static readonly Regex CreativeIdeasPattern = new(
        @"^What are some (?<style>[A-Za-z][A-Za-z -]*?) (?<item>[A-Za-z][A-Za-z -]*?) that you can make yourself\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static readonly Regex BeginnerGuidancePattern = new(
        @"^How do I start (?<activity>[A-Za-z][A-Za-z -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static readonly Regex WhyHarmfulPattern = new(
        @"^Why (?:(?:is|are) (?<subject>[A-Za-z][A-Za-z -]*?) bad for|(?<subject>[A-Za-z][A-Za-z -]*?) (?:is|are)? bad for) (?<target>[A-Za-z][A-Za-z -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static readonly Regex EventOutcomesPattern = new(
        @"^What happens when (?<event>[A-Za-z][A-Za-z -]*?)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static readonly Regex CausesPattern = new(
        @"^What causes (?<subject>[A-Za-z][A-Za-z -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static readonly Regex HeadquartersContributionPattern = new(
        @"^Who gave (?:the )?(?<recipient>[A-Za-z][A-Za-z0-9'’ -]*?) the (?<resource>[A-Za-z][A-Za-z -]*?) in (?<location>[A-Za-z][A-Za-z0-9'’ -]*?) to build (?:(?:their|its|the) )?(?<facility>[A-Za-z][A-Za-z -]*?)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static readonly Regex ComparePattern = new(
        @"^What distinguishes (?<subject>[A-Za-z][A-Za-z -]*?) from (?<other>[A-Za-z][A-Za-z -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

}
