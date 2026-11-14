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

    private static readonly Regex RelationActionPattern = new(
        @"^What does (?<subject>[A-Za-z][A-Za-z0-9'’! -]*?) (?<verb>[A-Za-z]+)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex InverseRelationPattern = new(
        @"^Who (?<verb>[A-Za-z]+) (?<subject>[A-Za-z][A-Za-z0-9'’! -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex DefinitionPattern = new(
        @"^What makes something an? (?<concept>[A-Za-z][A-Za-z -]*)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex WhyPattern = new(
        @"^Why is (?<subject>[A-Za-z][A-Za-z -]*?) an? (?<concept>[A-Za-z][A-Za-z -]*)\??$",
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
        string? sourceText = null) =>
        new(
            UtteranceKind.Question,
            new SemanticWorldModelQuestionFormula(kind, subject, concept, otherSubject),
            sourceText ?? string.Empty);

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
