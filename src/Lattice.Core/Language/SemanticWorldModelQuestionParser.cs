using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class SemanticWorldModelQuestionParser
{
    private static readonly Regex EnumerationRemainderPattern = new(
        @"^(?<member>[A-Z][A-Za-z'-]*)['’]s parents have (?<count>one|two|three|four|five|[0-9]+) daughters?: (?<known>[A-Z][A-Za-z'-]*(?:, [A-Z][A-Za-z'-]*)*), and what(?:'s|’s| is) the name of the (?<ordinal>first|second|third|fourth|fifth|[0-9]+(?:st|nd|rd|th)) daughter\??$",
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

    private static readonly Regex MechanismPattern = new(
        @"^How does (?<subject>[A-Za-z][A-Za-z -]*?) produce sound\??$",
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
        @"^Why (?<subject>[A-Za-z][A-Za-z -]*?) (?:is|are)? ?bad for (?<target>[A-Za-z][A-Za-z -]*)\??$",
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

    private static readonly Regex RelationSourcePattern = new(
        @"^(?:Who|What) (?!(?:is|are|was|were|be|being|been|has|have|does|do|did)\b)(?<verb>[a-z]+s) (?<object>(?:(?:the|a|an) )?(?!(?:when|where|why|how|to|for|from|with|by|in|on|at|of|that|which|who|what)\b)[A-Za-z]+(?: [A-Za-z]+){0,2})\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static bool TryParse(string source, out SemanticUtterance? utterance)
    {
        utterance = null;
        var text = source.Trim();

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

            if (expectedCount > 0 &&
                missingPosition == expectedCount &&
                namedMembers.Length == expectedCount - 1 &&
                !namedMembers.Contains(member, StringComparer.Ordinal))
            {
                utterance = new SemanticUtterance(
                    UtteranceKind.Question,
                    new EnumerationRemainderQuestionFormula(
                        member,
                        "daughters",
                        expectedCount,
                        missingPosition,
                        namedMembers),
                    source);
                return true;
            }
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
            _ => 0
        };

        if (number > 0)
        {
            return true;
        }

        var digits = Regex.Match(value, @"^(?<number>[0-9]+)(?:st|nd|rd|th)$");
        return digits.Success && int.TryParse(digits.Groups["number"].Value, out number) && number > 0;
    }

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
