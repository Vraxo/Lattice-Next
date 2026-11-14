using System.Text.RegularExpressions;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class SemanticWorldModelQuestionParser
{
    private static readonly Regex MembershipPattern = new(
        @"^Is (?<subject>[A-Za-z][A-Za-z0-9'’! -]*?) (?:a|an) (?<concept>[A-Za-z][A-Za-z -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex RelationObjectPattern = new(
        @"^How is (?<subject>[A-Za-z][A-Za-z0-9'’! -]*?) related to (?<other>[A-Za-z][A-Za-z0-9'’! -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex RelationLocationPattern = new(
        @"^Where is (?<subject>[A-Za-z][A-Za-z0-9'’! -]*?) (?<verb>[A-Za-z]+)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex TypedRelationActionPattern = new(
        @"^What (?<type>[A-Za-z][A-Za-z -]*) does (?<subject>[A-Za-z][A-Za-z0-9'’! -]*?) (?<verb>[A-Za-z]+)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex RelationActionPattern = new(
        @"^What does (?<subject>[A-Za-z][A-Za-z0-9'’! -]*?) (?<verb>[A-Za-z]+)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex HeadquartersContributionPattern = new(
        @"^Who gave (?<recipient>[A-Za-z][A-Za-z0-9'’ -]*?) (?:the )?(?<resource>land|property|site) in (?<location>[A-Za-z][A-Za-z0-9'’ -]*?) to build (?:(?:their|its|the) )?(?<facility>headquarters|HQ)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex InverseRelationPattern = new(
        @"^Who (?<verb>[A-Za-z]+) (?<subject>[A-Za-z][A-Za-z0-9'’! -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex TypedInverseRelationWithContextPattern = new(
        @"^Which (?<type>[A-Za-z][A-Za-z -]*?) of (?<context>[A-Za-z][A-Za-z0-9'’! -]*?) did (?<agent>[A-Za-z][A-Za-z0-9'’! -]*?) (?<verb>[A-Za-z]+)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex TypedInverseRelationPattern = new(
        @"^Which (?<type>[A-Za-z][A-Za-z -]*?) did (?<agent>[A-Za-z][A-Za-z0-9'’! -]*?) (?<verb>[A-Za-z]+)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex DefinitionPattern = new(
        @"^What makes something an? (?<concept>[A-Za-z][A-Za-z -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex WhyPattern = new(
        @"^Why is (?<subject>[A-Za-z][A-Za-z -]*?) an? (?<concept>[A-Za-z][A-Za-z -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex CapabilityExplanationPattern = new(
        @"^Why can (?<subject>[A-Za-z][A-Za-z0-9'’ -]*?) (?<verb>[A-Za-z]+)(?<context>.*?)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex MechanismPattern = new(
        @"^How does (?<subject>[A-Za-z][A-Za-z -]*?) produce sound\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex CausesPattern = new(
        @"^What causes (?<subject>[A-Za-z][A-Za-z -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ComparePattern = new(
        @"^What distinguishes (?<subject>[A-Za-z][A-Za-z -]*?) from (?<other>[A-Za-z][A-Za-z -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static bool TryParse(string source, out SemanticUtterance? utterance)
    {
        utterance = null;
        var text = source.Trim();

        var relationObject = RelationObjectPattern.Match(text);
        if (relationObject.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.RelationQuery,
                subject: Normalize(relationObject.Groups["subject"].Value),
                otherSubject: Normalize(relationObject.Groups["other"].Value),
                sourceText: source);
            return true;
        }

        var relationLocation = RelationLocationPattern.Match(text);
        if (relationLocation.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.RelationQuery,
                subject: Normalize(relationLocation.Groups["subject"].Value),
                concept: Normalize(relationLocation.Groups["verb"].Value),
                sourceText: source);
            return true;
        }

        var typedRelationAction = TypedRelationActionPattern.Match(text);
        if (typedRelationAction.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.RelationQuery,
                subject: Normalize(typedRelationAction.Groups["subject"].Value),
                concept: Normalize(typedRelationAction.Groups["verb"].Value),
                targetType: Normalize(typedRelationAction.Groups["type"].Value),
                sourceText: source);
            return true;
        }

        var relationAction = RelationActionPattern.Match(text);
        if (relationAction.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.RelationQuery,
                subject: Normalize(relationAction.Groups["subject"].Value),
                concept: Normalize(relationAction.Groups["verb"].Value),
                sourceText: source);
            return true;
        }

        var headquartersContribution = HeadquartersContributionPattern.Match(text);
        if (headquartersContribution.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.ContributionQuery,
                subject: Normalize(headquartersContribution.Groups["recipient"].Value),
                concept: Normalize(headquartersContribution.Groups["resource"].Value),
                otherSubject: Normalize(headquartersContribution.Groups["location"].Value),
                targetType: Normalize(headquartersContribution.Groups["facility"].Value),
                sourceText: source);
            return true;
        }

        var typedInverseWithContext = TypedInverseRelationWithContextPattern.Match(text);
        if (typedInverseWithContext.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.InverseRelation,
                subject: Normalize(typedInverseWithContext.Groups["agent"].Value),
                concept: Normalize(typedInverseWithContext.Groups["verb"].Value),
                otherSubject: Normalize(typedInverseWithContext.Groups["context"].Value),
                targetType: NormalizeType(typedInverseWithContext.Groups["type"].Value),
                sourceText: source);
            return true;
        }

        var typedInverse = TypedInverseRelationPattern.Match(text);
        if (typedInverse.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.InverseRelation,
                subject: Normalize(typedInverse.Groups["agent"].Value),
                concept: Normalize(typedInverse.Groups["verb"].Value),
                targetType: NormalizeType(typedInverse.Groups["type"].Value),
                sourceText: source);
            return true;
        }

        var inverseRelation = InverseRelationPattern.Match(text);
        if (inverseRelation.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.InverseRelation,
                subject: Normalize(inverseRelation.Groups["subject"].Value),
                concept: Normalize(inverseRelation.Groups["verb"].Value),
                sourceText: source);
            return true;
        }

        var membership = MembershipPattern.Match(text);
        if (membership.Success)
        {
            utterance = Create(
                SemanticWorldModelQuestionKind.Membership,
                subject: Normalize(membership.Groups["subject"].Value),
                concept: Normalize(membership.Groups["concept"].Value),
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

        return false;
    }

    private static SemanticUtterance Create(
        SemanticWorldModelQuestionKind kind,
        string subject = "",
        string? concept = null,
        string? otherSubject = null,
        string? targetType = null,
        string? sourceText = null) =>
        new(
            UtteranceKind.Question,
            new SemanticWorldModelQuestionFormula(kind, subject, concept, otherSubject, targetType),
            sourceText ?? string.Empty);

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
